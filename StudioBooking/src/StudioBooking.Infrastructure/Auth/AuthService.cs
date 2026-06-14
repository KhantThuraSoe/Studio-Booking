using Microsoft.EntityFrameworkCore;
using StudioBooking.Application.Interfaces;

namespace StudioBooking.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public AuthService(IUserRepository userRepository, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    public async Task<(bool Success, string? Token, string? Error)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, null, "Invalid email or password.");

        var token = _tokenService.GenerateToken(user.Id, user.Email, user.FullName);
        return (true, token, null);
    }
}
