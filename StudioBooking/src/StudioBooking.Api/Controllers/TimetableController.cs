using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioBooking.Application.DTOs.Timetable;
using StudioBooking.Application.Interfaces;

namespace StudioBooking.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/timetable")]
public class TimetableController : ControllerBase
{
    private readonly ITimetableService _timetableService;

    public TimetableController(ITimetableService timetableService) => _timetableService = timetableService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimetableScheduleDto>>> GetSchedules(
        [FromQuery] int? businessId,
        [FromQuery] DateTime? date,
        CancellationToken cancellationToken)
    {
        var schedules = await _timetableService.GetSchedulesAsync(businessId, date, cancellationToken);
        return Ok(schedules);
    }
}
