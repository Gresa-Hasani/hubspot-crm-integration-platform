using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CrmIntegration.Infrastructure.Security;

/// <summary>Thin wrapper over ASP.NET Core's PasswordHasher&lt;TUser&gt; (Microsoft.Extensions.Identity.Core, PBKDF2-based) — no custom hashing/salting. See docs/SECURITY.md "Password hashing".</summary>
public class PasswordHasherService : IPasswordHasherService
{
    private readonly IPasswordHasher<ApplicationUser> _hasher;

    public PasswordHasherService(IPasswordHasher<ApplicationUser> hasher)
    {
        _hasher = hasher;
    }

    public string HashPassword(string password) =>
        _hasher.HashPassword(new ApplicationUser(), password);

    public bool VerifyPassword(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(new ApplicationUser(), passwordHash, password) != PasswordVerificationResult.Failed;
}
