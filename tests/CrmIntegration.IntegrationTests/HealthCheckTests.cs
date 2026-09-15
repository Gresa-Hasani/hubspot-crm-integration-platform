using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CrmIntegration.IntegrationTests;

public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default",
                "Host=localhost;Port=5432;Database=crm_integration;Username=crm_user;Password=change-me-locally");
            builder.UseSetting("Jwt:Issuer", "CrmIntegrationPlatform");
            builder.UseSetting("Jwt:Audience", "CrmIntegrationPlatformClients");
            builder.UseSetting("Jwt:SigningKey", "test-signing-key-at-least-32-bytes-long!!");
        });
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsHealthy_WithoutTouchingTheDatabase()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
