using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CrmIntegration.IntegrationTests;

/// <summary>
/// Points the API under test at the docker-compose PostgreSQL instance started for local
/// development (see README "Running Phase 1 locally"). These tests require that container
/// to be running and migrated: `docker compose up -d postgres` then `dotnet ef database update`.
/// The default below matches docker-compose.yml's default port (5432); override with
/// CRM_INTEGRATION_TEST_CONNECTION_STRING if your local Postgres container uses a different one
/// (e.g. to avoid clashing with a native PostgreSQL install already using 5432).
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
}
