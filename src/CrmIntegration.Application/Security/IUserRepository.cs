using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Security;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="normalizedEmail">Already normalized by the caller — see Normalization.NormalizeEmail.</param>
    Task<ApplicationUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationUser>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);

    Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
