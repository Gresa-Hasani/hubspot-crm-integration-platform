using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CrmIntegration.IntegrationTests;

/// <summary>
/// Points the API under test at the docker-compose PostgreSQL instance started for local
/// development (see README "Running Phase 1 locally"). These tests require that container
/// to be running and migrated: `docker compose up -d postgres` then `dotnet ef database update`.
/// The default below matches docker-compose.yml's default port (5432); override with
/// CRM_INTEGRATION_TEST_CONNECTION_STRING if your local Postgres container uses a different one
/// (e.g. to avoid clashing with a native PostgreSQL install already using 5432).
///
/// Also provides Phase 8 authenticated-client helpers: <see cref="CreateAuthenticatedClientAsync"/>
/// seeds (or reuses) one fixed, idempotent test fixture user per role directly via
/// IUserRepository/IJwtTokenService — no HTTP login round-trip — and returns an HttpClient with a
/// real, pipeline-validated Bearer token attached. The login/refresh/logout HTTP flow itself is
/// tested explicitly and separately in AuthenticationApiTests.
/// </summary>
public class CrmApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Mirrors the API's JsonStringEnumConverter so response bodies (e.g. "stage": "ClosedWon") deserialize.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("CRM_INTEGRATION_TEST_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=crm_integration;Username=crm_user;Password=change-me-locally";

        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Jwt:Issuer", "CrmIntegrationPlatform");
        builder.UseSetting("Jwt:Audience", "CrmIntegrationPlatformClients");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-at-least-32-bytes-long!!");
    }

    /// <summary>Fixed test-fixture password for every per-role fixture user this helper creates — fictional, used only against the local test database. Exposed for AuthenticationApiTests' real HTTP login-flow test.</summary>
    public const string TestFixtureUserPassword = "Test-Fixture-Password-1";

    public static string TestFixtureEmail(UserRole role) => $"test.{role.ToString().ToLowerInvariant()}@crmplatform.test";

    /// <summary>Idempotently ensures the fixed fixture user for <paramref name="role"/> exists, active, with that exact role — creating or correcting it as needed (a prior RBAC test may have changed it).</summary>
    public async Task<ApplicationUser> EnsureTestUserAsync(UserRole role)
    {
        using var scope = Services.CreateScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var email = TestFixtureEmail(role);
        var user = await userRepository.GetByNormalizedEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(), Email = email, PasswordHash = passwordHasher.HashPassword(TestFixtureUserPassword),
                Role = role, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            await userRepository.AddAsync(user);
            await unitOfWork.SaveChangesAsync();
        }
        else if (user.Role != role || !user.IsActive)
        {
            user.Role = role;
            user.IsActive = true;
            await unitOfWork.SaveChangesAsync();
        }

        return user;
    }

    public async Task<string> GetAccessTokenAsync(UserRole role)
    {
        var user = await EnsureTestUserAsync(role);
        using var scope = Services.CreateScope();
        var jwtTokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        return jwtTokenService.CreateAccessToken(user).Token;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role)
    {
        var client = CreateClient();
        var token = await GetAccessTokenAsync(role);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
