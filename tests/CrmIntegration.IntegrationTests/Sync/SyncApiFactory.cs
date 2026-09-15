using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrmIntegration.IntegrationTests.Sync;

/// <summary>
/// Same real PostgreSQL wiring as CrmApiFactory, but replaces the real HubSpotClient with an
/// in-memory FakeHubSpotClient — sync-engine integration tests must never call the real HubSpot
/// API. One instance per test (not shared via IClassFixture) so each test's fake HubSpot state
/// starts empty.
/// </summary>
public class SyncApiFactory : CrmApiFactory
{
    public FakeHubSpotClient HubSpotClient { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHubSpotClient>();
            services.AddSingleton<IHubSpotClient>(HubSpotClient);
        });
    }
}
