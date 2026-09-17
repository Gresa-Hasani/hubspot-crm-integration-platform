using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Automation;

public interface IDealStageTransitionRepository
{
    Task AddAsync(DealStageTransition transition, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealStageTransition>> ListByDealAsync(Guid dealId, CancellationToken cancellationToken = default);
}
