using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Companies;

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Company company, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Companies whose normalized domain matches exactly (0, 1, or more — more than one is an ambiguous match).</summary>
    Task<IReadOnlyList<Company>> FindByNormalizedDomainAsync(string normalizedDomain, CancellationToken cancellationToken = default);

    /// <summary>Companies whose normalized name matches exactly. Name alone is never a confident (Exact) match — see docs/FIELD_MAPPING.md.</summary>
    Task<IReadOnlyList<Company>> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default);
}
