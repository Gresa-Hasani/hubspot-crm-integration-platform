using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CrmIntegration.Infrastructure.Security;

/// <summary>Issues signed JWT access tokens using System.IdentityModel.Tokens.Jwt (established .NET library) — no custom cryptography or signing logic. See docs/SECURITY.md "JWT design and claims".</summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public AccessTokenResult CreateAccessToken(ApplicationUser user)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.ExpirationMinutes);
        var jti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var raw = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(raw, expires, jti);
    }
}
