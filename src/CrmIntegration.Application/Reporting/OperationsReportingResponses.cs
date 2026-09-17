namespace CrmIntegration.Application.Reporting;

public record StatusCountMetric(string Status, int Count);

public record OperationalHealthResponse(
    IReadOnlyList<StatusCountMetric> SyncJobsByStatus,
    IReadOnlyList<StatusCountMetric> IntegrationEventsByStatus,
    IReadOnlyList<StatusCountMetric> AutomationExecutionsByStatus,
    int SyncJobFailuresLast24Hours,
    int IntegrationEventFailuresLast24Hours,
    int AutomationFailuresLast24Hours);
