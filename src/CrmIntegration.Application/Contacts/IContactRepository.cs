using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Contacts;

public interface IContactRepository
{
    Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Contacts whose normalized email matches exactly (0, 1, or more — more than one is an ambiguous match).</summary>
    Task<IReadOnlyList<Contact>> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
}
