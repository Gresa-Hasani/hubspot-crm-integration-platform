using CrmIntegration.Domain.Entities;
using CrmIntegration.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace CrmIntegration.UnitTests.Security;

public class PasswordHasherServiceTests
{
    private static PasswordHasherService CreateService() =>
        new(new PasswordHasher<ApplicationUser>());

    [Fact]
    public void HashPassword_NeverReturnsThePlaintextPassword()
    {
        var service = CreateService();

        var hash = service.HashPassword("CorrectHorse123");

        Assert.NotEqual("CorrectHorse123", hash);
        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    [Fact]
    public void HashPassword_ProducesDifferentHashes_ForTheSamePassword_DueToRandomSalt()
    {
        var service = CreateService();

        var hash1 = service.HashPassword("CorrectHorse123");
        var hash2 = service.HashPassword("CorrectHorse123");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_AcceptsTheCorrectPassword()
    {
        var service = CreateService();
        var hash = service.HashPassword("CorrectHorse123");

        Assert.True(service.VerifyPassword(hash, "CorrectHorse123"));
    }

    [Fact]
    public void VerifyPassword_RejectsAnIncorrectPassword()
    {
        var service = CreateService();
        var hash = service.HashPassword("CorrectHorse123");

        Assert.False(service.VerifyPassword(hash, "WrongPassword456"));
    }
}
