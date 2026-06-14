using StudioBooking.Application.DTOs.Waitlist;
using StudioBooking.Application.Exceptions;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Entities;
using StudioBooking.Domain.Enums;

namespace StudioBooking.Application.Services;

public class WaitlistService : IWaitlistService
{
    private readonly ITimetableRepository _timetableRepository;
    private readonly IWaitlistRepository _waitlistRepository;
    private readonly PackageValidationService _packageValidationService;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;

    public WaitlistService(
        ITimetableRepository timetableRepository,
        IWaitlistRepository waitlistRepository,
        PackageValidationService packageValidationService,
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService)
    {
        _timetableRepository = timetableRepository;
        _waitlistRepository = waitlistRepository;
        _packageValidationService = packageValidationService;
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
    }

    public async Task<WaitlistResponse> JoinWaitlistAsync(
        int userId,
        JoinWaitlistRequest request,
        CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var schedule = await _timetableRepository.GetByIdAsync(request.ScheduleId, cancellationToken)
            ?? throw new BusinessRuleException("SCHEDULE_NOT_FOUND", "Timetable schedule not found.");

        if (schedule.EndTime <= utcNow)
            throw new BusinessRuleException("SCHEDULE_ENDED", "Cannot join waitlist for a class that has ended.");

        var confirmedCount = await _timetableRepository.GetConfirmedBookingCountAsync(schedule.Id, cancellationToken);
        if (confirmedCount < schedule.MaxSlots)
            throw new BusinessRuleException("SLOTS_AVAILABLE", "Schedule is not full. Book directly instead.");

        await _packageValidationService.ValidatePackageForScheduleAsync(
            userId, request.PackageId, schedule.BusinessId, utcNow, cancellationToken);

        if (await _bookingRepository.HasOverlappingBookingAsync(userId, schedule.StartTime, schedule.EndTime, cancellationToken: cancellationToken))
            throw new BusinessRuleException("SCHEDULE_OVERLAP", "You already have a booking that overlaps with this schedule.");

        var existing = await _waitlistRepository.GetActiveByUserAndScheduleAsync(userId, schedule.Id, cancellationToken);
        if (existing is not null)
            throw new BusinessRuleException("ALREADY_ON_WAITLIST", "You are already on the waitlist for this schedule.");

        var entry = new WaitlistEntry
        {
            UserId = userId,
            TimetableScheduleId = schedule.Id,
            PackageId = request.PackageId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = utcNow
        };

        await _waitlistRepository.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var position = await _waitlistRepository.GetQueuePositionAsync(schedule.Id, entry.Id, cancellationToken);
        await _cacheService.RemoveByPrefixAsync("timetable:", cancellationToken);

        return new WaitlistResponse(
            entry.Id,
            schedule.Id,
            request.PackageId,
            entry.Status.ToString(),
            entry.JoinedAt,
            position);
    }

    public async Task ExpireEndedWaitlistsAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var expiredEntries = await _waitlistRepository.GetExpiredWaitingEntriesAsync(utcNow, cancellationToken);

        if (expiredEntries.Count == 0)
            return;

        foreach (var entry in expiredEntries)
            entry.Status = WaitlistStatus.Expired;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
