namespace CrmIntegration.Application.Configuration;

/// <summary>
/// One-time initial Admin provisioning — see docs/SECURITY.md "Admin bootstrap". Only used when
/// the ApplicationUsers table is empty; both values must be set or bootstrap is skipped entirely.
/// Never hardcode real values here or in appsettings.json — set via environment/.env (gitignored)
/// or user-secrets.
/// </summary>
public class BootstrapAdminOptions
{
    public const string SectionName = "BootstrapAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
}
