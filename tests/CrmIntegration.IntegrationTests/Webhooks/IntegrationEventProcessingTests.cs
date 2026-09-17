using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Webhooks;

/// <summary>
/// Verifies the processor actually drives the real Phase 4 sync services (against a fake HubSpot
/// client, real PostgreSQL) end to end — not a reimplementation of sync logic.
/// </summary>
public class IntegrationEventProcessingTests
{
    [Fact]
    public async Task ProcessAsync_ForCompanyEvent_CreatesInternalCompany_ViaPhase4Sync()
    {
        using var factory = new WebhookApiFactory();
        using var scope = factory.Services.CreateScope();
        var hubSpotClient = factory.HubSpotClient;
        var record = await hubSpotClient.CreateCompanyAsync(new Dictionary<string, string?>
        {
            ["name"] = "Webhook Test Co " + Guid.NewGuid().ToString("N")[..8]
        });

        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
        var integrationEvent = new IntegrationEvent
        {
            Id = Guid.NewGuid(),
            ExternalEventId = "test-" + Guid.NewGuid().ToString("N"),
            EventType = "company.creation",
            EntityType = EntityType.Company,
            EntityId = record.Id,
            Status = IntegrationEventStatus.Processing,
            CorrelationId = "corr-1",
            ReceivedAt = DateTime.UtcNow
        };
        await repository.TryAddAsync(integrationEvent);

        var processor = scope.ServiceProvider.GetRequiredService<IIntegrationEventProcessor>();
        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(IntegrationEventStatus.Processed, integrationEvent.Status);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mapping = await db.EntityMappings.FirstOrDefaultAsync(m => m.ExternalId == record.Id && m.EntityType == EntityType.Company);
        Assert.NotNull(mapping);
    }

    [Fact]
    public async Task ProcessAsync_MarksFailed_WhenPhase4SyncHitsADeterministicFailure()
    {
        // The referenced HubSpot company doesn't exist in the fake client, so Phase 4's
        // CompanySyncService.SyncFromHubSpotAsync throws a deterministic DomainValidationException
        // (not one of the 3 retryable HubSpot exception types) — the processor must not retry this
        // at the event level either. Genuine transient-retry-exhaustion -> DeadLettered is covered
        // with full control over Phase 4's outcome in IntegrationEventProcessorTests (unit).
        using var factory = new WebhookApiFactory();
        using var scope = factory.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
        var integrationEvent = new IntegrationEvent
        {
            Id = Guid.NewGuid(),
            ExternalEventId = "test-" + Guid.NewGuid().ToString("N"),
            EventType = "company.creation",
            EntityType = EntityType.Company,
            EntityId = "does-not-exist-in-fake-hubspot",
            Status = IntegrationEventStatus.Processing,
            CorrelationId = "corr-1",
            ReceivedAt = DateTime.UtcNow
        };
        await repository.TryAddAsync(integrationEvent);

        var processor = scope.ServiceProvider.GetRequiredService<IIntegrationEventProcessor>();
        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(IntegrationEventStatus.Failed, integrationEvent.Status);
        Assert.Equal(1, integrationEvent.AttemptCount);
    }

    [Fact]
    public async Task DeadLetteredEvent_CanBeManuallyRetried_ThroughApi()
    {
        using var factory = new WebhookApiFactory();
        var client = factory.CreateClient();
        Guid integrationEventId;

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
            var integrationEvent = new IntegrationEvent
            {
                Id = Guid.NewGuid(),
                ExternalEventId = "test-" + Guid.NewGuid().ToString("N"),
                EventType = "company.creation",
                EntityType = EntityType.Company,
                EntityId = "1",
                Status = IntegrationEventStatus.DeadLettered,
                AttemptCount = 3,
                FailureCategory = "Transient",
                LastError = "simulated",
                CorrelationId = "corr-1",
                ReceivedAt = DateTime.UtcNow
            };
            await repository.TryAddAsync(integrationEvent);
            integrationEventId = integrationEvent.Id;
        }

        var response = await client.PostAsync($"/api/webhooks/events/{integrationEventId}/retry", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db.IntegrationEvents.AsNoTracking().FirstAsync(e => e.Id == integrationEventId);
        Assert.Equal(IntegrationEventStatus.Received, reloaded.Status);
        Assert.Equal(0, reloaded.AttemptCount);
    }

    [Fact]
    public async Task RetryingNonDeadLetteredEvent_ReturnsBadRequest()
    {
        using var factory = new WebhookApiFactory();
        var client = factory.CreateClient();
        Guid integrationEventId;

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
            var integrationEvent = new IntegrationEvent
            {
                Id = Guid.NewGuid(), ExternalEventId = "test-" + Guid.NewGuid().ToString("N"),
                EventType = "company.creation", EntityType = EntityType.Company, EntityId = "1",
                Status = IntegrationEventStatus.Processed, CorrelationId = "corr-1", ReceivedAt = DateTime.UtcNow
            };
            await repository.TryAddAsync(integrationEvent);
            integrationEventId = integrationEvent.Id;
        }

        var response = await client.PostAsync($"/api/webhooks/events/{integrationEventId}/retry", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
