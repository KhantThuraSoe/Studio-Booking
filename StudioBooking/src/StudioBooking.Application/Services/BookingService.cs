using StudioBooking.Application.DTOs.Bookings;
using StudioBooking.Application.Exceptions;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;

namespace StudioBooking.Application.Services;

public class BookingService : IBookingService
{
    private readonly ITimetableRepository _timetableRepository;
    private readonly IPackageRepository _packageRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly PackageValidationService _packageValidationService;
    private readonly ISlotReservationService _slotReservationService;
    private readonly IWaitlistPromotionService _waitlistPromotionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public BookingService(
        ITimetableRepository timetableRepository,
        IPackageRepository packageRepository,
        IBookingRepository bookingRepository,
        PackageValidationService packageValidationService,
        ISlotReservationService slotReservationService,
        IWaitlistPromotionService waitlistPromotionService,
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _timetableRepository = timetableRepository;
        _packageRepository = packageRepository;
        _bookingRepository = bookingRepository;
        _packageValidationService = packageValidationService;
        _slotReservationService = slotReservationService;
        _waitlistPromotionService = waitlistPromotionService;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<BookingResponse> BookClassAsync(int userId, BookClassRequest request, CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var schedule = await _timetableRepository.GetByIdAsync(request.ScheduleId, cancellationToken)
            ?? throw new BusinessRuleException("SCHEDULE_NOT_FOUND", "Timetable schedule not found.");

        if (schedule.StartTime <= utcNow)
            throw new BusinessRuleException("SCHEDULE_STARTED", "Cannot book a class that has already started.");

        var package = await _packageValidationService.GetValidatedPackageAsync(
            userId, request.PackageId, schedule.BusinessId, utcNow, cancellationToken);

        if (await _bookingRepository.HasOverlappingBookingAsync(userId, schedule.StartTime, schedule.EndTime, cancellationToken: cancellationToken))
            throw new BusinessRuleException("SCHEDULE_OVERLAP", "You already have a booking that overlaps with this schedule.");

        var confirmedCount = await _timetableRepository.GetConfirmedBookingCountAsync(schedule.Id, cancellationToken);
        if (confirmedCount >= schedule.MaxSlots)
            throw new BusinessRuleException("SCHEDULE_FULL", "Timetable schedule is full. Join the waitlist instead.");

        var slotReserved = await _slotReservationService.TryReserveSlotAsync(
            schedule.Id, schedule.MaxSlots, confirmedCount, cancellationToken);

        if (!slotReserved)
            throw new BusinessRuleException("SLOT_UNAVAILABLE", "No slots available due to concurrent booking activity.");

        try
        {
            confirmedCount = await _timetableRepository.GetConfirmedBookingCountAsync(schedule.Id, cancellationToken);
            if (confirmedCount >= schedule.MaxSlots)
            {
                await _slotReservationService.ReleaseSlotAsync(schedule.Id, cancellationToken);
                throw new BusinessRuleException("SCHEDULE_FULL", "Timetable schedule is full. Join the waitlist instead.");
            }

            package.RemainingCredits -= 1;

            var booking = new Booking
            {
                UserId = userId,
                TimetableScheduleId = schedule.Id,
                PackageId = package.Id,
                Status = BookingStatus.Confirmed,
                BookedAt = utcNow
            };

            await _bookingRepository.AddAsync(booking, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await InvalidateCachesAsync(userId, schedule.BusinessId, schedule.StartTime, cancellationToken);

            return new BookingResponse(
                booking.Id,
                schedule.Id,
                package.Id,
                booking.Status.ToString(),
                booking.BookedAt,
                package.RemainingCredits);
        }
        catch
        {
            await _slotReservationService.ReleaseSlotAsync(schedule.Id, cancellationToken);
            throw;
        }
    }

    public async Task<CancelBookingResponse> CancelBookingAsync(
        int userId,
        CancelBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var booking = await _bookingRepository.GetByIdForUserAsync(request.BookingId, userId, cancellationToken)
            ?? throw new BusinessRuleException("BOOKING_NOT_FOUND", "Booking not found.");

        if (booking.Status == BookingStatus.Cancelled)
            throw new BusinessRuleException("BOOKING_ALREADY_CANCELLED", "Booking is already cancelled.");

        var schedule = booking.TimetableSchedule;
        var package = booking.Package;

        var hoursUntilStart = (schedule.StartTime - utcNow).TotalHours;
        var creditRefunded = hoursUntilStart > 4;

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = utcNow;

        if (creditRefunded)
            package.RemainingCredits += 1;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _slotReservationService.ReleaseSlotAsync(schedule.Id, cancellationToken);

        var (waitlistEntryId, promotedBookingId) = await _waitlistPromotionService.PromoteNextEligibleAsync(
            schedule.Id, cancellationToken);

        if (promotedBookingId.HasValue)
            await _slotReservationService.SyncSlotCountAsync(
                schedule.Id,
                await _timetableRepository.GetConfirmedBookingCountAsync(schedule.Id, cancellationToken),
                cancellationToken);

        await InvalidateCachesAsync(userId, schedule.BusinessId, schedule.StartTime, cancellationToken);

        return new CancelBookingResponse(
            booking.Id,
            creditRefunded,
            package.RemainingCredits,
            waitlistEntryId,
            promotedBookingId);
    }

    private async Task InvalidateCachesAsync(int userId, int businessId, DateTime scheduleStart, CancellationToken cancellationToken)
    {
        await _cacheService.RemoveAsync($"packages:user:{userId}", cancellationToken);
        await _cacheService.RemoveByPrefixAsync("timetable:", cancellationToken);
    }
}
