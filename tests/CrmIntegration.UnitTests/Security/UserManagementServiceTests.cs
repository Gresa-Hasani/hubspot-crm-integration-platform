using CrmIntegration.Application.Common;
using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Security;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace CrmIntegration.UnitTests.Security;

public class UserManagementServiceTests
{
    private static (UserManagementService Service, FakeUserRepository UserRepository) CreateService(DateTimeOffset now)
    {
        var userRepository = new FakeUserRepository();
        var passwordHasher = new PasswordHasherService(new PasswordHasher<ApplicationUser>());
        var service = new UserManagementService(
            userRepository, passwordHasher, new FakeAuditLogRepository(), new FakeUnitOfWork(), new FixedTimeProvider(now));
        return (service, userRepository);
    }

    private static ApplicationUser SeedAdmin(FakeUserRepository repo, bool isActive = true)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), Email = $"admin.{Guid.NewGuid():N}@example.com", PasswordHash = "x",
            Role = UserRole.Admin, IsActive = isActive, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        repo.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesUser_WithHashedPassword()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);

        var result = await service.CreateAsync("new.user@example.com", "CorrectHorse123", UserRole.ReadOnly);

        var stored = Assert.Single(userRepository.Users);
        Assert.Equal("new.user@example.com", result.Email);
        Assert.NotEqual("CorrectHorse123", stored.PasswordHash);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task CreateAsync_DuplicateNormalizedEmail_Throws()
    {
        var (service, _) = CreateService(DateTimeOffset.UtcNow);
        await service.CreateAsync("Dup@Example.com", "CorrectHorse123", UserRole.Sales);

        await Assert.ThrowsAsync<DuplicateEmailException>(() => service.CreateAsync("dup@example.com", "AnotherPass456", UserRole.Sales));
    }

    [Fact]
    public async Task CreateAsync_WeakPassword_ThrowsDomainValidationException()
    {
        var (service, _) = CreateService(DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.CreateAsync("user@example.com", "weak", UserRole.Sales));
    }

    [Fact]
    public async Task ChangeRoleAsync_UnknownUser_ThrowsEntityNotFound()
    {
        var (service, _) = CreateService(DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.ChangeRoleAsync(Guid.NewGuid(), UserRole.Sales));
    }

    [Fact]
    public async Task ChangeRoleAsync_DemotingTheLastActiveAdmin_Throws()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var admin = SeedAdmin(userRepository);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.ChangeRoleAsync(admin.Id, UserRole.Sales));
        Assert.Equal(UserRole.Admin, admin.Role); // unchanged
    }

    [Fact]
    public async Task ChangeRoleAsync_DemotingOneOfMultipleActiveAdmins_Succeeds()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var admin1 = SeedAdmin(userRepository);
        SeedAdmin(userRepository); // a second active Admin

        var result = await service.ChangeRoleAsync(admin1.Id, UserRole.Operations);

        Assert.Equal(UserRole.Operations, result.Role);
    }

    [Fact]
    public async Task ChangeRoleAsync_PromotingANonAdmin_NeverTriggersLastAdminProtection()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var salesUser = new ApplicationUser { Id = Guid.NewGuid(), Email = "sales@example.com", PasswordHash = "x", Role = UserRole.Sales, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        userRepository.Users.Add(salesUser);

        var result = await service.ChangeRoleAsync(salesUser.Id, UserRole.Admin);

        Assert.Equal(UserRole.Admin, result.Role);
    }

    [Fact]
    public async Task SetActiveStatusAsync_DeactivatingTheLastActiveAdmin_Throws()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var admin = SeedAdmin(userRepository);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.SetActiveStatusAsync(admin.Id, false));
        Assert.True(admin.IsActive); // unchanged
    }

    [Fact]
    public async Task SetActiveStatusAsync_DeactivatingOneOfMultipleActiveAdmins_Succeeds()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var admin1 = SeedAdmin(userRepository);
        SeedAdmin(userRepository);

        var result = await service.SetActiveStatusAsync(admin1.Id, false);

        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task SetActiveStatusAsync_ReactivatingAnAdmin_NeverTriggersLastAdminProtection()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var admin = SeedAdmin(userRepository, isActive: false);

        var result = await service.SetActiveStatusAsync(admin.Id, true);

        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task ResetPasswordAsync_WeakPassword_Throws()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = SeedAdmin(userRepository);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.ResetPasswordAsync(user.Id, "weak"));
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidPassword_ChangesTheHash()
    {
        var (service, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = SeedAdmin(userRepository);
        var originalHash = user.PasswordHash;

        await service.ResetPasswordAsync(user.Id, "NewCorrectHorse456");

        Assert.NotEqual(originalHash, user.PasswordHash);
    }
}
