using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Automation;

public interface IContactLifecycleTransitionRepository
{
    Task AddAsync(ContactLifecycleTransition transition, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactLifecycleTransition>> ListByContactAsync(Guid contactId, CancellationToken cancellationToken = default);
}
