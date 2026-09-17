namespace CrmIntegration.Application.Reporting;

public interface IOperationsReportingService
{
    Task<OperationalHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
}
