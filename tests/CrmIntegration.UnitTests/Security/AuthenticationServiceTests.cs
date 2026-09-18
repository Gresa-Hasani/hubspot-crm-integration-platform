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

public class AuthenticationServiceTests
{
    private static (AuthenticationService Service, FakeUserRepository UserRepository, FakeAuditLogRepository AuditLogRepository)
        CreateService(DateTimeOffset now)
    {
        var userRepository = new FakeUserRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var passwordHasher = new PasswordHasherService(new PasswordHasher<ApplicationUser>());
        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = "TestIssuer", Audience = "TestAudience",
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!", ExpirationMinutes = 30
        });
        var timeProvider = new FixedTimeProvider(now);
        var jwtTokenService = new JwtTokenService(jwtOptions, timeProvider);
        var refreshTokenRepository = new FakeRefreshTokenRepository();
        var refreshTokenService = new RefreshTokenService(
            refreshTokenRepository, userRepository, auditLogRepository, new FakeUnitOfWork(), timeProvider, jwtOptions);

        var service = new AuthenticationService(
            userRepository, passwordHasher, jwtTokenService, refreshTokenService,
            auditLogRepository, new FakeUnitOfWork(), timeProvider, NullLogger<AuthenticationService>.Instance);

        return (service, userRepository, auditLogRepository);
    }

    private static ApplicationUser SeedUser(FakeUserRepository repo, string email, string password, bool isActive = true, UserRole role = UserRole.Sales)
    {
        var hasher = new PasswordHasherService(new PasswordHasher<ApplicationUser>());
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), Email = email, PasswordHash = hasher.HashPassword(password),
            Role = role, IsActive = isActive, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        repo.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokensAndUpdatesLastLoginAt()
    {
        var (service, userRepository, _) = CreateService(DateTimeOffset.UtcNow);
        var user = SeedUser(userRepository, "user@example.com", "CorrectHorse123");

        var result = await service.LoginAsync("user@example.com", "CorrectHorse123");

        Assert.Equal(user.Id, result.UserId);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.NotNull(user.LastLoginAt);
    }

    [Fact]
    public async Task LoginAsync_NormalizesEmailCase_AndWhitespace()
    {
        var (service, userRepository, _) = CreateService(DateTimeOffset.UtcNow);
        SeedUser(userRepository, "user@example.com", "CorrectHorse123");

        var result = await service.LoginAsync("  User@Example.COM  ", "CorrectHorse123");

        Assert.Equal("user@example.com", result.Email);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ThrowsGenericInvalidCredentials()
    {
        var (service, _, _) = CreateService(DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync("nobody@example.com", "whatever123"));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsTheSameGenericException_AsUnknownEmail()
    {
        var (service, userRepository, _) = CreateService(DateTimeOffset.UtcNow);
        SeedUser(userRepository, "user@example.com", "CorrectHorse123");

        var ex1 = await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync("user@example.com", "WrongPassword456"));
        var ex2 = await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync("nobody@example.com", "whatever123"));

        Assert.Equal(ex1.Message, ex2.Message); // never distinguishable
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ThrowsInvalidCredentials()
    {
        var (service, userRepository, _) = CreateService(DateTimeOffset.UtcNow);
        SeedUser(userRepository, "user@example.com", "CorrectHorse123", isActive: false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync("user@example.com", "CorrectHorse123"));
    }

    [Fact]
    public async Task LoginAsync_Failure_WritesALoginFailedAuditEntry_WithoutThePassword()
    {
        var (service, userRepository, auditLogRepository) = CreateService(DateTimeOffset.UtcNow);
        SeedUser(userRepository, "user@example.com", "CorrectHorse123");

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync("user@example.com", "WrongPassword456"));

        var entry = Assert.Single(auditLogRepository.Entries, e => e.Action == "LoginFailed");
        Assert.DoesNotContain("WrongPassword456", entry.Metadata ?? string.Empty);
        Assert.DoesNotContain("WrongPassword456", entry.EntityId);
    }

    [Fact]
    public async Task LoginAsync_Success_WritesALoginSucceededAuditEntry()
    {
        var (service, userRepository, auditLogRepository) = CreateService(DateTimeOffset.UtcNow);
        SeedUser(userRepository, "user@example.com", "CorrectHorse123");

        await service.LoginAsync("user@example.com", "CorrectHorse123");

        Assert.Contains(auditLogRepository.Entries, e => e.Action == "LoginSucceeded");
    }
}
