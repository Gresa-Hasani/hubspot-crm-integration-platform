using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Contacts;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
