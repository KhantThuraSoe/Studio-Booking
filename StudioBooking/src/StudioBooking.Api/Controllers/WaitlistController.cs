using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioBooking.Application.DTOs.Waitlist;
using StudioBooking.Application.Interfaces;

namespace StudioBooking.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/waitlist")]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistService _waitlistService;

    public WaitlistController(IWaitlistService waitlistService) => _waitlistService = waitlistService;

    [HttpPost]
    public async Task<ActionResult<WaitlistResponse>> JoinWaitlist(
        [FromBody] JoinWaitlistRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _waitlistService.JoinWaitlistAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
