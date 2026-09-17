using CrmIntegration.IntegrationTests.Sync;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CrmIntegration.IntegrationTests.Webhooks;

/// <summary>
/// Extends SyncApiFactory (real PostgreSQL + fake HubSpot client) with a known
/// HubSpot:WebhookSigningSecret so tests can compute valid v3 signatures, and disables the
/// background IntegrationEventProcessingWorker so HTTP-level ingestion tests are deterministic —
/// processing is driven explicitly in tests that need it.
/// </summary>
public class WebhookApiFactory : SyncApiFactory
{
    public const string SigningSecret = "integration-test-webhook-signing-secret";

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("HubSpot:WebhookSigningSecret", SigningSecret);
        builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
    }
}
