using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Webhooks;

public class IntegrationEventProcessorTests
{
    private readonly FakeContactSyncService _contactSync = new();
    private readonly FakeCompanySyncService _companySync = new();
    private readonly FakeDealSyncService _dealSync = new();
    private readonly FakeAuditLogRepository _auditLogRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private IntegrationEventProcessor CreateProcessor(int maxAttempts = 3) => new(
        _contactSync, _companySync, _dealSync, _auditLogRepository, _unitOfWork,
        new FixedTimeProvider(DateTimeOffset.UtcNow),
        Options.Create(new WebhookOptions { MaxProcessingAttempts = maxAttempts }),
        NullLogger<IntegrationEventProcessor>.Instance);

    private static IntegrationEvent CreateEvent(EntityType entityType, string objectId = "10") => new()
    {
        Id = Guid.NewGuid(),
        ExternalEventId = "1",
        EventType = $"{entityType}.propertyChange",
        EntityType = entityType,
        EntityId = objectId,
        Status = IntegrationEventStatus.Processing,
        CorrelationId = "corr-1",
        ReceivedAt = DateTime.UtcNow
    };

    private static SyncJob SucceededJob() => new() { Id = Guid.NewGuid(), Status = SyncStatus.Succeeded };
    private static SyncJob DeadLetteredJob(string category = "Transient") => new() { Id = Guid.NewGuid(), Status = SyncStatus.DeadLettered, FailureCategory = category };

    [Fact]
    public async Task ProcessAsync_RoutesContactEvent_ToContactSyncService()
    {
        var integrationEvent = CreateEvent(EntityType.Contact, "10");
        _contactSync.OnSyncFromHubSpot = (id, corr) => { Assert.Equal("10", id); Assert.Equal("corr-1", corr); return SucceededJob(); };
        var processor = CreateProcessor();

        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(1, _contactSync.CallCount);
        Assert.Equal(0, _companySync.CallCount);
        Assert.Equal(0, _dealSync.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RoutesCompanyEvent_ToCompanySyncService()
    {
        var integrationEvent = CreateEvent(EntityType.Company);
        _companySync.OnSyncFromHubSpot = (_, _) => SucceededJob();
        var processor = CreateProcessor();

        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(1, _companySync.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_RoutesDealEvent_ToDealSyncService()
    {
        var integrationEvent = CreateEvent(EntityType.Deal);
        _dealSync.OnSyncFromHubSpot = (_, _) => SucceededJob();
        var processor = CreateProcessor();

        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(1, _dealSync.CallCount);
    }

    [Fact]
    public async Task ProcessAsync_MarksProcessed_OnSyncSuccess()
    {
        var integrationEvent = CreateEvent(EntityType.Contact);
        _contactSync.OnSyncFromHubSpot = (_, _) => SucceededJob();
        var processor = CreateProcessor();

        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(IntegrationEventStatus.Processed, integrationEvent.Status);
        Assert.Equal(1, integrationEvent.AttemptCount);
        Assert.NotNull(integrationEvent.ProcessedAt);
        Assert.Contains(_auditLogRepository.Entries, e => e.Action == "WebhookProcessingSucceeded");
    }

    [Fact]
    public async Task ProcessAsync_MarksFailed_ForDeterministicException_WithoutConsumingRetryBudget()
    {
        var integrationEvent = CreateEvent(EntityType.Contact);
        _contactSync.OnSyncFromHubSpot = (_, _) => throw new HubSpotBadRequestException("bad data", null);
        var processor = CreateProcessor(maxAttempts: 3);

        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(IntegrationEventStatus.Failed, integrationEvent.Status);
        Assert.Equal(1, integrationEvent.AttemptCount);
        Assert.Equal(nameof(HubSpotBadRequestException), integrationEvent.FailureCategory);
    }

    [Fact]
    public async Task ProcessAsync_LeavesEventReceived_WhenTransientFailureHasAttemptsRemaining()
    {
        var integrationEvent = CreateEvent(EntityType.Contact);
        _contactSync.OnSyncFromHubSpot = (_, _) => DeadLetteredJob("Transient");
        var processor = CreateProcessor(maxAttempts: 3);

        await processor.ProcessAsync(integrationEvent);

        Assert.Equal(IntegrationEventStatus.Received, integrationEvent.Status);
        Assert.Equal(1, integrationEvent.AttemptCount);
        Assert.Equal("Transient", integrationEvent.FailureCategory);
    }

    [Fact]
    public async Task ProcessAsync_DeadLetters_WhenTransientFailureExhaustsMaxAttempts()
    {
        var integrationEvent = CreateEvent(EntityType.Contact);
        integrationEvent.AttemptCount = 2; // already tried twice
        _contactSync.OnSyncFromHubSpot = (_, _) => DeadLetteredJob("Transient");
        var processor = CreateProcessor(maxAttempts: 3);

        await processor.ProcessAsync(integrationEvent); // this is the 3rd attempt

        Assert.Equal(IntegrationEventStatus.DeadLettered, integrationEvent.Status);
        Assert.Equal(3, integrationEvent.AttemptCount);
        Assert.Contains(_auditLogRepository.Entries, e => e.Action.Contains("DeadLettered"));
    }
}
