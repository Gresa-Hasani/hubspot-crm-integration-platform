namespace CrmIntegration.Domain.Entities;

/// <summary>
/// A rotating refresh token. Only the SHA-256 hash of the raw token is ever persisted — the raw
/// value is returned to the client exactly once, at issuance, and never logged or stored. See
/// docs/SECURITY.md "Refresh-token design".
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>SHA-256 hash (hex) of the raw refresh token — unique. The raw token itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when this token is used (rotated) or explicitly revoked (logout). A non-null value means this token can never be used again.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>The token that replaced this one on rotation, when known — lets the rotation chain be traced for auditing/replay detection.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}
