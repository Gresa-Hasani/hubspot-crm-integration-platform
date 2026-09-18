using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Security;

public interface IRefreshTokenRepository
{
    /// <param name="tokenHash">SHA-256 hex hash of the raw token — never the raw token itself.</param>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically revokes the token identified by <paramref name="tokenId"/> ONLY if it is still
    /// un-revoked (a single conditional UPDATE, not load-then-save) — the concurrency-safe claim
    /// that makes rotation safe under a genuine simultaneous replay, mirroring Phase 5's
    /// IntegrationEvent claiming pattern (docs/WEBHOOKS.md). Returns false if another caller has
    /// already revoked it first (lost the race) — the caller must not proceed as if it won.
    /// </summary>
    Task<bool> TryRevokeAsync(Guid tokenId, DateTime revokedAtUtc, Guid? replacedByTokenId, CancellationToken cancellationToken = default);
}
