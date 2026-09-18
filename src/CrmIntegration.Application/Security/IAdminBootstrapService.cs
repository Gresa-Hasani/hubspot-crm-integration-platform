namespace CrmIntegration.Application.Security;

/// <summary>
/// One-time initial Admin provisioning, run once at startup — see docs/SECURITY.md "Admin
/// bootstrap". Only creates a user when the ApplicationUsers table is empty AND both
/// BootstrapAdmin:Email/Password are configured; never overwrites an existing user.
/// </summary>
public interface IAdminBootstrapService
{
    Task BootstrapAsync(CancellationToken cancellationToken = default);
}
