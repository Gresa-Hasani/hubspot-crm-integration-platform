using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using CrmIntegration.Domain.Enums;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CrmIntegration.IntegrationTests.Security;

/// <summary>
/// Exercises the REAL ASP.NET Core JWT authentication pipeline (not just token-generation helper
/// methods) against a protected endpoint (GET /api/contacts, which requires CanReadCrm). Every
/// token here is hand-crafted with System.IdentityModel.Tokens.Jwt to test one specific validation
/// failure mode, matching CrmApiFactory's fixed test Issuer/Audience/SigningKey where a "valid"
/// baseline is needed. See docs/SECURITY.md "Access-token validation".
/// </summary>
public class JwtPipelineTests
{
    private const string ProtectedEndpoint = "/api/contacts";
    private const string TestIssuer = "CrmIntegrationPlatform";
    private const string TestAudience = "CrmIntegrationPlatformClients";
    private const string TestSigningKey = "test-signing-key-at-least-32-bytes-long!!";

    private static string BuildToken(
        string signingKey, string issuer, string audience, DateTime notBefore, DateTime expires, UserRole role = UserRole.Admin)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "pipeline-test@example.com"),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, notBefore, expires, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static HttpClient ClientWithToken(CrmApiFactory factory, string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    [Fact]
    public async Task NoToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = ClientWithToken(factory, null);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = ClientWithToken(factory, "this-is-not-a-jwt-at-all");

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var now = DateTime.UtcNow;
        var token = BuildToken(TestSigningKey, TestIssuer, TestAudience, now.AddHours(-2), now.AddHours(-1));
        var client = ClientWithToken(factory, token);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuer_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var now = DateTime.UtcNow;
        var token = BuildToken(TestSigningKey, "SomeOtherIssuer", TestAudience, now, now.AddHours(1));
        var client = ClientWithToken(factory, token);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongAudience_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var now = DateTime.UtcNow;
        var token = BuildToken(TestSigningKey, TestIssuer, "SomeOtherAudience", now, now.AddHours(1));
        var client = ClientWithToken(factory, token);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongSigningKey_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var now = DateTime.UtcNow;
        var token = BuildToken("a-completely-different-signing-key-32bytes!", TestIssuer, TestAudience, now, now.AddHours(1));
        var client = ClientWithToken(factory, token);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedSignature_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var now = DateTime.UtcNow;
        var validToken = BuildToken(TestSigningKey, TestIssuer, TestAudience, now, now.AddHours(1));
        var parts = validToken.Split('.');
        // Flip the last character of the signature segment — payload/header untouched, signature no longer verifies.
        var lastChar = parts[2][^1];
        var flipped = lastChar == 'A' ? 'B' : 'A';
        var tamperedSignature = parts[2][..^1] + flipped;
        var tamperedToken = $"{parts[0]}.{parts[1]}.{tamperedSignature}";
        var client = ClientWithToken(factory, tamperedToken);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_MatchingIssuerAudienceSignatureAndRole_IsAccepted()
    {
        using var factory = new CrmApiFactory();
        var now = DateTime.UtcNow;
        var token = BuildToken(TestSigningKey, TestIssuer, TestAudience, now, now.AddHours(1), UserRole.ReadOnly);
        var client = ClientWithToken(factory, token);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnsignedToken_None_Algorithm_IsRejected()
    {
        using var factory = new CrmApiFactory();
        var now = DateTime.UtcNow;
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, UserRole.Admin.ToString()) };
        var unsignedToken = new JwtSecurityToken(TestIssuer, TestAudience, claims, now, now.AddHours(1)); // no SigningCredentials
        var raw = new JwtSecurityTokenHandler().WriteToken(unsignedToken);
        var client = ClientWithToken(factory, raw);

        var response = await client.GetAsync(ProtectedEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
