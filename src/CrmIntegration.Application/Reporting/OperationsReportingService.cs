namespace CrmIntegration.Application.Reporting;

public class OperationsReportingService : IOperationsReportingService
{
    private static readonly TimeSpan RecentWindow = TimeSpan.FromHours(24);

    private readonly IOperationsReportingRepository _repository;
    private readonly TimeProvider _timeProvider;

    public OperationsReportingService(IOperationsReportingRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<OperationalHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var since = _timeProvider.GetUtcNow().UtcDateTime - RecentWindow;

        var syncStatuses = await _repository.GetSyncJobCountsByStatusAsync(cancellationToken);
        var eventStatuses = await _repository.GetIntegrationEventCountsByStatusAsync(cancellationToken);
        var automationStatuses = await _repository.GetAutomationExecutionCountsByStatusAsync(cancellationToken);

        var syncFailures = await _repository.CountSyncJobFailuresSinceAsync(since, cancellationToken);
        var eventFailures = await _repository.CountIntegrationEventFailuresSinceAsync(since, cancellationToken);
        var automationFailures = await _repository.CountAutomationFailuresSinceAsync(since, cancellationToken);

        return new OperationalHealthResponse(
            syncStatuses, eventStatuses, automationStatuses,
            syncFailures, eventFailures, automationFailures);
    }
}
