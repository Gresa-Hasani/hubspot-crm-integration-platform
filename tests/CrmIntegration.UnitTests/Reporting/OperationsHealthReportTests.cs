using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class OperationsHealthReportTests
{
    [Fact]
    public async Task GetHealthAsync_EmptyDatabase_ReturnsEmptyBreakdowns()
    {
        var repo = new FakeOperationsReportingRepository();
        var service = new OperationsReportingService(repo, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.GetHealthAsync();

        Assert.Empty(result.SyncJobsByStatus);
        Assert.Empty(result.IntegrationEventsByStatus);
        Assert.Empty(result.AutomationExecutionsByStatus);
        Assert.Equal(0, result.SyncJobFailuresLast24Hours);
    }

    [Fact]
    public async Task GetHealthAsync_AggregatesSyncJobsByStatus()
    {
        var repo = new FakeOperationsReportingRepository();
        repo.SyncJobs.Add(new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.Succeeded, CorrelationId = "c", StartedAt = DateTime.UtcNow });
        repo.SyncJobs.Add(new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.Failed, CorrelationId = "c", StartedAt = DateTime.UtcNow });
        repo.SyncJobs.Add(new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.DeadLettered, CorrelationId = "c", StartedAt = DateTime.UtcNow });
        var service = new OperationsReportingService(repo, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.GetHealthAsync();

        Assert.Equal(1, result.SyncJobsByStatus.Single(s => s.Status == "Succeeded").Count);
        Assert.Equal(1, result.SyncJobsByStatus.Single(s => s.Status == "Failed").Count);
        Assert.Equal(1, result.SyncJobsByStatus.Single(s => s.Status == "DeadLettered").Count);
    }

    [Fact]
    public async Task GetHealthAsync_AggregatesIntegrationEventsByStatus()
    {
        var repo = new FakeOperationsReportingRepository();
        repo.IntegrationEvents.Add(new IntegrationEvent { Id = Guid.NewGuid(), ExternalEventId = "e1", EventType = "t", EntityType = EntityType.Deal, EntityId = "1", Status = IntegrationEventStatus.Processed, CorrelationId = "c", ReceivedAt = DateTime.UtcNow });
        repo.IntegrationEvents.Add(new IntegrationEvent { Id = Guid.NewGuid(), ExternalEventId = "e2", EventType = "t", EntityType = EntityType.Deal, EntityId = "2", Status = IntegrationEventStatus.Failed, CorrelationId = "c", ReceivedAt = DateTime.UtcNow });
        var service = new OperationsReportingService(repo, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.GetHealthAsync();

        Assert.Equal(1, result.IntegrationEventsByStatus.Single(s => s.Status == "Processed").Count);
        Assert.Equal(1, result.IntegrationEventsByStatus.Single(s => s.Status == "Failed").Count);
    }

    [Fact]
    public async Task GetHealthAsync_AggregatesAutomationExecutionsByStatus()
    {
        var repo = new FakeOperationsReportingRepository();
        repo.AutomationExecutions.Add(new AutomationExecution { Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal, EntityId = Guid.NewGuid(), IdempotencyKey = "k1", Status = AutomationStatus.Succeeded, CorrelationId = "c", StartedAt = DateTime.UtcNow });
        repo.AutomationExecutions.Add(new AutomationExecution { Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal, EntityId = Guid.NewGuid(), IdempotencyKey = "k2", Status = AutomationStatus.Skipped, CorrelationId = "c", StartedAt = DateTime.UtcNow });
        repo.AutomationExecutions.Add(new AutomationExecution { Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal, EntityId = Guid.NewGuid(), IdempotencyKey = "k3", Status = AutomationStatus.Failed, CorrelationId = "c", StartedAt = DateTime.UtcNow });
        var service = new OperationsReportingService(repo, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.GetHealthAsync();

        Assert.Equal(1, result.AutomationExecutionsByStatus.Single(s => s.Status == "Succeeded").Count);
        Assert.Equal(1, result.AutomationExecutionsByStatus.Single(s => s.Status == "Skipped").Count);
        Assert.Equal(1, result.AutomationExecutionsByStatus.Single(s => s.Status == "Failed").Count);
    }

    [Fact]
    public async Task GetHealthAsync_CountsRecentFailures_WithinLast24Hours_ExcludingOlder()
    {
        var now = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var repo = new FakeOperationsReportingRepository();
        repo.SyncJobs.Add(new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.Failed, CorrelationId = "c", StartedAt = now.UtcDateTime.AddHours(-1) }); // recent
        repo.SyncJobs.Add(new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.Failed, CorrelationId = "c", StartedAt = now.UtcDateTime.AddHours(-30) }); // too old
        var service = new OperationsReportingService(repo, new FixedTimeProvider(now));

        var result = await service.GetHealthAsync();

        Assert.Equal(1, result.SyncJobFailuresLast24Hours);
    }
}
