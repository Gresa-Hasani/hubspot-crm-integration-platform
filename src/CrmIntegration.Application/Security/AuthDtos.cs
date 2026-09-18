using System.ComponentModel.DataAnnotations;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Security;

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record LoginResponse(
    string AccessToken, DateTime AccessTokenExpiresAt,
    string RefreshToken, DateTime RefreshTokenExpiresAt,
    Guid UserId, string Email, UserRole Role)
{
    public static LoginResponse FromResult(LoginResult r) =>
        new(r.AccessToken, r.AccessTokenExpiresAtUtc, r.RefreshToken, r.RefreshTokenExpiresAtUtc, r.UserId, r.Email, r.Role);
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public record RefreshResponse(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, DateTime RefreshTokenExpiresAt);

public class LogoutRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public record CurrentUserResponse(Guid Id, string Email, UserRole Role);
