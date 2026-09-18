using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Security;

public record LoginResult(
    string AccessToken, DateTime AccessTokenExpiresAtUtc,
    string RefreshToken, DateTime RefreshTokenExpiresAtUtc,
    Guid UserId, string Email, UserRole Role);

/// <summary>Login orchestration: credential verification, LastLoginAt update, safe audit logging, and token issuance (delegated to IJwtTokenService/IRefreshTokenService). See docs/SECURITY.md "Login behavior".</summary>
public interface IAuthenticationService
{
    /// <summary>Throws InvalidCredentialsException for any failure (unknown email, wrong password, inactive account) — never distinguishable from the response.</summary>
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}
