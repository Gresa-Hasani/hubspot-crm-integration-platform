using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await _context.RefreshTokens.AddAsync(token, cancellationToken);

    public async Task<bool> TryRevokeAsync(Guid tokenId, DateTime revokedAtUtc, Guid? replacedByTokenId, CancellationToken cancellationToken = default)
    {
        var rowsAffected = await _context.RefreshTokens
            .Where(t => t.Id == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, revokedAtUtc)
                .SetProperty(t => t.ReplacedByTokenId, replacedByTokenId), cancellationToken);
        return rowsAffected == 1;
    }
}
