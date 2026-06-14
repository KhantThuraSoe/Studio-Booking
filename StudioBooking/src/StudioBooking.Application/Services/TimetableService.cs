using StudioBooking.Application.DTOs.Timetable;
using StudioBooking.Application.Interfaces;
using StudioBooking.Domain.Enums;

namespace StudioBooking.Application.Services;

public class TimetableService : ITimetableService
{
    private readonly ITimetableRepository _timetableRepository;
    private readonly ICacheService _cacheService;

    public TimetableService(ITimetableRepository timetableRepository, ICacheService cacheService)
    {
        _timetableRepository = timetableRepository;
        _cacheService = cacheService;
    }

    public async Task<IReadOnlyList<TimetableScheduleDto>> GetSchedulesAsync(
        int? businessId,
        DateTime? date,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"timetable:{businessId}:{date:yyyy-MM-dd}";
        var cached = await _cacheService.GetAsync<List<TimetableScheduleDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var schedules = await _timetableRepository.GetSchedulesAsync(businessId, date, cancellationToken);

        var result = schedules.Select(s =>
        {
            var attendance = s.Bookings.Count(b => b.Status == BookingStatus.Confirmed);
            return new TimetableScheduleDto(
                s.Id,
                s.ClassName,
                s.InstructorName,
                s.StartTime,
                s.EndTime,
                attendance,
                Math.Max(0, s.MaxSlots - attendance),
                s.Business.Name);
        }).ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);
        return result;
    }
}
