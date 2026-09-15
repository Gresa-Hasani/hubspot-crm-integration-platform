using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Deals;

public interface IDealRepository
{
    Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Deal>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Deal deal, CancellationToken cancellationToken = default);
}
