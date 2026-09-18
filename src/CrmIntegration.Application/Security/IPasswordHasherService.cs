namespace CrmIntegration.Application.Security;

/// <summary>Thin wrapper over ASP.NET Core's PasswordHasher&lt;TUser&gt; (Microsoft.Extensions.Identity.Core) — no custom cryptography. See docs/SECURITY.md "Password hashing".</summary>
public interface IPasswordHasherService
{
    string HashPassword(string password);

    /// <returns>True if <paramref name="password"/> matches <paramref name="passwordHash"/>.</returns>
    bool VerifyPassword(string passwordHash, string password);
}
