using StudioBooking.Application.DTOs.Bookings;
using StudioBooking.Application.Exceptions;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;

namespace StudioBooking.Application.Services;

public class WaitlistPromotionService : IWaitlistPromotionService
{
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly IPackageRepository _packageRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public WaitlistPromotionService(
        IWaitlistRepository waitlistRepository,
        IPackageRepository packageRepository,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _waitlistRepository = waitlistRepository;
        _packageRepository = packageRepository;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<(int? WaitlistEntryId, int? BookingId)> PromoteNextEligibleAsync(
        int scheduleId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var waiting = await _waitlistRepository.GetWaitingByScheduleAsync(scheduleId, cancellationToken);

        foreach (var entry in waiting)
        {
            var package = await _packageRepository.GetByIdForUserAsync(entry.PackageId, entry.UserId, cancellationToken);
            if (package is null || package.IsExpired(utcNow) || !package.HasAvailableCredits)
            {
                entry.Status = WaitlistStatus.Expired;
                continue;
            }

            if (await _bookingRepository.HasOverlappingBookingAsync(
                    entry.UserId,
                    entry.TimetableSchedule.StartTime,
                    entry.TimetableSchedule.EndTime,
                    cancellationToken: cancellationToken))
            {
                entry.Status = WaitlistStatus.Expired;
                continue;
            }

            package.RemainingCredits -= 1;

            var booking = new Booking
            {
                UserId = entry.UserId,
                TimetableScheduleId = scheduleId,
                PackageId = package.Id,
                Status = BookingStatus.Confirmed,
                BookedAt = utcNow
            };

            entry.Status = WaitlistStatus.Promoted;
            entry.PromotedAt = utcNow;

            await _bookingRepository.AddAsync(booking, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _cacheService.RemoveAsync($"packages:user:{entry.UserId}", cancellationToken);
            await _cacheService.RemoveByPrefixAsync("timetable:", cancellationToken);

            return (entry.Id, booking.Id);
        }

        if (waiting.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (null, null);
    }
}
