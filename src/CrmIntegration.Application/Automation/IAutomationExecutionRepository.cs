using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Automation;

public interface IAutomationExecutionRepository
{
    Task AddAsync(AutomationExecution execution, CancellationToken cancellationToken = default);
    Task<AutomationExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AutomationExecution?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationExecution>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
}
