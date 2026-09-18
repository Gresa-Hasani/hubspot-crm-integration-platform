using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Security;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Security;

public class AdminBootstrapServiceTests
{
    private static AdminBootstrapService CreateService(FakeUserRepository userRepository, BootstrapAdminOptions options) =>
        new(userRepository, new PasswordHasherService(new PasswordHasher<ApplicationUser>()), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UtcNow), Options.Create(options), NullLogger<AdminBootstrapService>.Instance);

    [Fact]
    public async Task BootstrapAsync_EmptyDatabase_WithConfiguredCredentials_CreatesActiveAdmin()
    {
        var userRepository = new FakeUserRepository();
        var service = CreateService(userRepository, new BootstrapAdminOptions { Email = "admin@example.com", Password = "BootstrapPass1" });

        await service.BootstrapAsync();

        var created = Assert.Single(userRepository.Users);
        Assert.Equal("admin@example.com", created.Email);
        Assert.Equal(UserRole.Admin, created.Role);
        Assert.True(created.IsActive);
        Assert.NotEqual("BootstrapPass1", created.PasswordHash);
    }

    [Fact]
    public async Task BootstrapAsync_UsersAlreadyExist_NeverOverwritesOrAddsAnother()
    {
        var userRepository = new FakeUserRepository();
        var existing = new ApplicationUser { Id = Guid.NewGuid(), Email = "existing@example.com", PasswordHash = "hash", Role = UserRole.ReadOnly, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        userRepository.Users.Add(existing);
        var service = CreateService(userRepository, new BootstrapAdminOptions { Email = "admin@example.com", Password = "BootstrapPass1" });

        await service.BootstrapAsync();

        Assert.Single(userRepository.Users); // still just the pre-existing one
        Assert.Equal("existing@example.com", userRepository.Users[0].Email);
    }

    [Fact]
    public async Task BootstrapAsync_NoConfiguredEmailOrPassword_DoesNothing()
    {
        var userRepository = new FakeUserRepository();
        var service = CreateService(userRepository, new BootstrapAdminOptions { Email = null, Password = null });

        await service.BootstrapAsync();

        Assert.Empty(userRepository.Users);
    }

    [Fact]
    public async Task BootstrapAsync_OnlyEmailConfigured_WithoutPassword_DoesNothing()
    {
        var userRepository = new FakeUserRepository();
        var service = CreateService(userRepository, new BootstrapAdminOptions { Email = "admin@example.com", Password = null });

        await service.BootstrapAsync();

        Assert.Empty(userRepository.Users);
    }

    [Fact]
    public async Task BootstrapAsync_PasswordDoesNotMeetPolicy_SkipsWithoutThrowing()
    {
        var userRepository = new FakeUserRepository();
        var service = CreateService(userRepository, new BootstrapAdminOptions { Email = "admin@example.com", Password = "weak" });

        await service.BootstrapAsync();

        Assert.Empty(userRepository.Users);
    }

    [Fact]
    public async Task BootstrapAsync_NormalizesTheConfiguredEmail()
    {
        var userRepository = new FakeUserRepository();
        var service = CreateService(userRepository, new BootstrapAdminOptions { Email = "  Admin@EXAMPLE.com  ", Password = "BootstrapPass1" });

        await service.BootstrapAsync();

        Assert.Equal("admin@example.com", userRepository.Users[0].Email);
    }
}
