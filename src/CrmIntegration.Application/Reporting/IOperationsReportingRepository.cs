namespace CrmIntegration.Application.Reporting;

/// <summary>Read-only aggregate queries over SyncJob/IntegrationEvent/AutomationExecution for the operational sales-health report. Never returns raw payloads, error messages, or exception details.</summary>
public interface IOperationsReportingRepository
{
    Task<IReadOnlyList<StatusCountMetric>> GetSyncJobCountsByStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatusCountMetric>> GetIntegrationEventCountsByStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatusCountMetric>> GetAutomationExecutionCountsByStatusAsync(CancellationToken cancellationToken = default);

    Task<int> CountSyncJobFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<int> CountIntegrationEventFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<int> CountAutomationFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
}
