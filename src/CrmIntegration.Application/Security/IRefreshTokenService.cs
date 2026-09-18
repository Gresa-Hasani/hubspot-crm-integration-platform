namespace CrmIntegration.Application.Security;

public record RefreshTokenIssueResult(string RawToken, Guid TokenId, DateTime ExpiresAtUtc);

public record RefreshTokenRotationResult(Guid UserId, RefreshTokenIssueResult NewToken);

/// <summary>
/// Issues, rotates, and revokes refresh tokens. Only a SHA-256 hash of the raw token is ever
/// persisted — see docs/SECURITY.md "Refresh-token design" for the full rotation/replay-
/// protection model.
/// </summary>
public interface IRefreshTokenService
{
    Task<RefreshTokenIssueResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Validates the raw token, revokes it, and issues a new one in its place (rotation). Throws InvalidRefreshTokenException if the token is unknown, expired, or already revoked/rotated — including a replay of an already-rotated token.</summary>
    Task<RefreshTokenRotationResult> RotateAsync(string rawRefreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes the given token (logout) without issuing a replacement. Throws InvalidRefreshTokenException if it doesn't belong to <paramref name="userId"/> or is already invalid.</summary>
    Task RevokeAsync(string rawRefreshToken, Guid userId, CancellationToken cancellationToken = default);
}
