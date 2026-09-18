using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.UnitTests.TestDoubles;

public class FakeUserRepository : IUserRepository
{
    public List<ApplicationUser> Users { get; } = new();

    public Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task<ApplicationUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == normalizedEmail));

    public Task<IReadOnlyList<ApplicationUser>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ApplicationUser>>(Users.OrderBy(u => u.Email).ToList());

    public Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Count > 0);

    public Task<int> CountActiveAdminsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Count(u => u.Role == UserRole.Admin && u.IsActive));

    public Task AddAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }
}

public class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = new();

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<bool> TryRevokeAsync(Guid tokenId, DateTime revokedAtUtc, Guid? replacedByTokenId, CancellationToken cancellationToken = default)
    {
        var token = Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token is null || token.RevokedAt is not null)
        {
            return Task.FromResult(false);
        }

        token.RevokedAt = revokedAtUtc;
        token.ReplacedByTokenId = replacedByTokenId;
        return Task.FromResult(true);
    }
}
