using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Automation;

/// <summary>Manually re-runs a Failed AutomationExecution. Succeeded/Skipped executions are never re-run.</summary>
public interface IAutomationRetryService
{
    Task<AutomationExecution> RetryAsync(Guid executionId, CancellationToken cancellationToken = default);
}
