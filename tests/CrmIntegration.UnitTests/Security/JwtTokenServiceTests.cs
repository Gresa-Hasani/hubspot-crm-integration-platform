using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Security;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Security;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(DateTimeOffset now, out JwtOptions options)
    {
        options = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
            ExpirationMinutes = 30
        };
        return new JwtTokenService(Options.Create(options), new FixedTimeProvider(now));
    }

    private static ApplicationUser NewUser() => new()
    {
        Id = Guid.NewGuid(), Email = "user@example.com", Role = UserRole.Sales,
        PasswordHash = "irrelevant", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public void CreateAccessToken_IncludesSubjectEmailRoleAndJti()
    {
        var service = CreateService(DateTimeOffset.UtcNow, out _);
        var user = NewUser();

        var result = service.CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(UserRole.Sales.ToString(), jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.False(string.IsNullOrWhiteSpace(jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
        Assert.Equal(result.Jti, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void CreateAccessToken_UsesConfiguredIssuerAndAudience()
    {
        var service = CreateService(DateTimeOffset.UtcNow, out var options);
        var result = service.CreateAccessToken(NewUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(options.Issuer, jwt.Issuer);
        Assert.Equal(options.Audience, jwt.Audiences.Single());
    }

    [Fact]
    public void CreateAccessToken_ExpiresAfterConfiguredMinutes_FromUtcNow()
    {
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(now, out var options);

        var result = service.CreateAccessToken(NewUser());

        Assert.Equal(now.UtcDateTime.AddMinutes(options.ExpirationMinutes), result.ExpiresAtUtc);
    }

    [Fact]
    public void CreateAccessToken_GeneratesADifferentJti_EachCall()
    {
        var service = CreateService(DateTimeOffset.UtcNow, out _);
        var user = NewUser();

        var first = service.CreateAccessToken(user);
        var second = service.CreateAccessToken(user);

        Assert.NotEqual(first.Jti, second.Jti);
    }
}
