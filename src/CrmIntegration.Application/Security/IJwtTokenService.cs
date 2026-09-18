using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Security;

public record AccessTokenResult(string Token, DateTime ExpiresAtUtc, string Jti);

/// <summary>Issues signed JWT access tokens — see docs/SECURITY.md "JWT design and claims". Uses System.IdentityModel.Tokens.Jwt (established .NET library), never custom cryptography.</summary>
public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(ApplicationUser user);
}
