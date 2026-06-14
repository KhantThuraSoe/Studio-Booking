using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioBooking.Application.DTOs.Bookings;
using StudioBooking.Application.Interfaces;

namespace StudioBooking.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService) => _bookingService = bookingService;

    [HttpPost]
    public async Task<ActionResult<BookingResponse>> BookClass(
        [FromBody] BookClassRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _bookingService.BookClassAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("cancel")]
    public async Task<ActionResult<CancelBookingResponse>> CancelBooking(
        [FromBody] CancelBookingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _bookingService.CancelBookingAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
