using System.Security.Cryptography;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Security;

/// <summary>
/// Cryptographically random refresh tokens (256-bit, from RandomNumberGenerator — no custom
/// cryptography), stored only as a SHA-256 hash. See docs/SECURITY.md "Refresh-token design" for
/// the rotation/replay-protection model this implements.
/// </summary>
public class RefreshTokenService : IRefreshTokenService
{
    private const int TokenSizeBytes = 32;

    private readonly IRefreshTokenRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly JwtOptions _options;

    public RefreshTokenService(
        IRefreshTokenRepository repository,
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        IOptions<JwtOptions> options)
    {
        _repository = repository;
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<RefreshTokenIssueResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (rawToken, hash) = GenerateToken();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddDays(_options.RefreshTokenExpirationDays);

        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        await _repository.AddAsync(token, cancellationToken);
        await _auditLogRepository.AddAsync(AuditEntry("RefreshTokenIssued", userId, now), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshTokenIssueResult(rawToken, token.Id, expiresAt);
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawRefreshToken);
        var existing = await _repository.GetByTokenHashAsync(hash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (!existing.IsActive)
        {
            // Includes replay of an already-rotated/revoked token, and a genuinely expired one —
            // never distinguished in the response (docs/SECURITY.md).
            throw new InvalidRefreshTokenException();
        }

        // Checked before rotating (not after) so a deactivated user's session can never mint a
        // fresh, live refresh token that would sit unused until/unless the account is reactivated.
        var user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        var newTokenId = Guid.NewGuid();

        // Atomic conditional claim FIRST (a single "UPDATE ... WHERE RevokedAt IS NULL", not
        // load-then-save) — this, not the IsActive check above, is what actually makes rotation
        // safe under a genuine simultaneous replay: two concurrent callers can both pass the
        // IsActive check before either commits, but only one UPDATE can win this claim. The loser
        // must not proceed to mint/persist a new token at all. See docs/SECURITY.md "Refresh
        // rotation / replay protection".
        var claimed = await _repository.TryRevokeAsync(existing.Id, now, newTokenId, cancellationToken);
        if (!claimed)
        {
            throw new InvalidRefreshTokenException();
        }

        var (newRawToken, newHash) = GenerateToken();
        var expiresAt = now.AddDays(_options.RefreshTokenExpirationDays);
        var newToken = new RefreshToken
        {
            Id = newTokenId,
            UserId = existing.UserId,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        await _repository.AddAsync(newToken, cancellationToken);
        await _auditLogRepository.AddAsync(AuditEntry("RefreshTokenRotated", existing.UserId, now), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshTokenRotationResult(existing.UserId, new RefreshTokenIssueResult(newRawToken, newToken.Id, expiresAt));
    }

    public async Task RevokeAsync(string rawRefreshToken, Guid userId, CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawRefreshToken);
        var existing = await _repository.GetByTokenHashAsync(hash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        if (existing.UserId != userId || !existing.IsActive)
        {
            throw new InvalidRefreshTokenException();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Same atomic conditional claim as rotation — a concurrent logout racing a concurrent
        // refresh (or two concurrent logouts) can only have one winner.
        if (!await _repository.TryRevokeAsync(existing.Id, now, replacedByTokenId: null, cancellationToken))
        {
            throw new InvalidRefreshTokenException();
        }

        await _auditLogRepository.AddAsync(AuditEntry("Logout", userId, now), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static (string RawToken, string Hash) GenerateToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(TokenSizeBytes);
        var rawToken = Convert.ToBase64String(rawBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (rawToken, Hash(rawToken));
    }

    private static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }

    private static AuditLog AuditEntry(string action, Guid userId, DateTime at) => new()
    {
        Id = Guid.NewGuid(),
        Action = action,
        EntityType = "ApplicationUser",
        EntityId = userId.ToString(),
        Source = "Authentication",
        CorrelationId = Guid.NewGuid().ToString(),
        Timestamp = at
    };
}
