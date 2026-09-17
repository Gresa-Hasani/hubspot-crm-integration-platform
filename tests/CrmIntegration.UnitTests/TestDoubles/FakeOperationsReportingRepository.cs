using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.UnitTests.TestDoubles;

public class FakeOperationsReportingRepository : IOperationsReportingRepository
{
    public List<SyncJob> SyncJobs { get; } = new();
    public List<IntegrationEvent> IntegrationEvents { get; } = new();
    public List<AutomationExecution> AutomationExecutions { get; } = new();

    public Task<IReadOnlyList<StatusCountMetric>> GetSyncJobCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StatusCountMetric>>(
            SyncJobs.GroupBy(j => j.Status).Select(g => new StatusCountMetric(g.Key.ToString(), g.Count())).ToList());

    public Task<IReadOnlyList<StatusCountMetric>> GetIntegrationEventCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StatusCountMetric>>(
            IntegrationEvents.GroupBy(e => e.Status).Select(g => new StatusCountMetric(g.Key.ToString(), g.Count())).ToList());

    public Task<IReadOnlyList<StatusCountMetric>> GetAutomationExecutionCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StatusCountMetric>>(
            AutomationExecutions.GroupBy(a => a.Status).Select(g => new StatusCountMetric(g.Key.ToString(), g.Count())).ToList());

    public Task<int> CountSyncJobFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(SyncJobs.Count(j => (j.Status == SyncStatus.Failed || j.Status == SyncStatus.DeadLettered) && j.StartedAt >= sinceUtc));

    public Task<int> CountIntegrationEventFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(IntegrationEvents.Count(e => (e.Status == IntegrationEventStatus.Failed || e.Status == IntegrationEventStatus.DeadLettered) && e.ReceivedAt >= sinceUtc));

    public Task<int> CountAutomationFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(AutomationExecutions.Count(a => a.Status == AutomationStatus.Failed && a.StartedAt >= sinceUtc));
}
