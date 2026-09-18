using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Domain.Entities;

/// <summary>
/// A local/internal application account (not a CRM Contact) — see docs/SECURITY.md. Provisioned
/// only by an Admin or the one-time bootstrap (Program.cs); there is no public self-registration.
/// </summary>
public class ApplicationUser
{
    public Guid Id { get; set; }

    /// <summary>Normalized (trimmed, lower-invariant) — the unique lookup key. See Normalization.NormalizeEmail.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Produced by IPasswordHasherService (ASP.NET Core's PasswordHasher&lt;TUser&gt;) — never plaintext, never returned by any API.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.ReadOnly;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
