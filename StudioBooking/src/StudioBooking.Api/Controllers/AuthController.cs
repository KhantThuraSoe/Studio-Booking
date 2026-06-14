using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioBooking.Application.DTOs.Auth;
using StudioBooking.Application.Interfaces;

namespace StudioBooking.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepository;

    public AuthController(IAuthService authService, IUserRepository userRepository)
    {
        _authService = authService;
        _userRepository = userRepository;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var (success, token, error) = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (!success)
            return Unauthorized(new { errorCode = "INVALID_CREDENTIALS", message = error });

        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        var expiresAt = DateTime.UtcNow.AddMinutes(120);

        return Ok(new LoginResponse(token!, user!.Id, user.Email, user.FullName, expiresAt));
    }
}
