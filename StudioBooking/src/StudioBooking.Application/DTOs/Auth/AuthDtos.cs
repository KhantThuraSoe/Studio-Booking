namespace StudioBooking.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, int UserId, string Email, string FullName, DateTime ExpiresAt);

public record RegisterRequest(string Email, string Password, string FullName);
