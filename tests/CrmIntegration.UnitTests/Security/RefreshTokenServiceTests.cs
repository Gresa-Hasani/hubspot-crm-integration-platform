using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Security;

public class RefreshTokenServiceTests
{
    private static (RefreshTokenService Service, FakeRefreshTokenRepository TokenRepository, FakeUserRepository UserRepository)
        CreateService(DateTimeOffset now, int expirationDays = 14)
    {
        var tokenRepository = new FakeRefreshTokenRepository();
        var userRepository = new FakeUserRepository();
        var options = Options.Create(new JwtOptions { RefreshTokenExpirationDays = expirationDays });
        var service = new RefreshTokenService(
            tokenRepository, userRepository, new FakeAuditLogRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(now), options);
        return (service, tokenRepository, userRepository);
    }

    private static ApplicationUser ActiveUser() => new()
    {
        Id = Guid.NewGuid(), Email = "user@example.com", Role = UserRole.Sales, PasswordHash = "x",
        IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task IssueAsync_NeverPersistsTheRawToken()
    {
        var (service, tokenRepository, _) = CreateService(DateTimeOffset.UtcNow);

        var result = await service.IssueAsync(Guid.NewGuid());

        var stored = Assert.Single(tokenRepository.Tokens);
        Assert.NotEqual(result.RawToken, stored.TokenHash);
        Assert.NotEmpty(stored.TokenHash);
    }

    [Fact]
    public async Task IssueAsync_SetsExpirationFromConfiguredDays()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (service, _, _) = CreateService(now, expirationDays: 7);

        var result = await service.IssueAsync(Guid.NewGuid());

        Assert.Equal(now.UtcDateTime.AddDays(7), result.ExpiresAtUtc);
    }

    [Fact]
    public async Task RotateAsync_ValidToken_IssuesANewTokenAndRevokesTheOld()
    {
        var (service, tokenRepository, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);

        var rotation = await service.RotateAsync(issued.RawToken);

        Assert.Equal(user.Id, rotation.UserId);
        Assert.NotEqual(issued.RawToken, rotation.NewToken.RawToken);
        var original = tokenRepository.Tokens.Single(t => t.Id == issued.TokenId);
        Assert.NotNull(original.RevokedAt);
        Assert.Equal(2, tokenRepository.Tokens.Count);
    }

    [Fact]
    public async Task RotateAsync_ReplayOfAnAlreadyRotatedToken_Throws()
    {
        var (service, _, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);
        await service.RotateAsync(issued.RawToken); // first rotation succeeds

        // Replaying the same (now-revoked) raw token must fail — this is the core replay-attack defense.
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RotateAsync(issued.RawToken));
    }

    [Fact]
    public async Task RotateAsync_UnknownToken_Throws()
    {
        var (service, _, _) = CreateService(DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RotateAsync("not-a-real-token"));
    }

    [Fact]
    public async Task RotateAsync_ExpiredToken_Throws()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var (service, tokenRepository, userRepository) = CreateService(now, expirationDays: 1);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);

        // Advance a second RefreshTokenService instance's clock past expiration to validate.
        var laterService = new RefreshTokenService(
            tokenRepository, userRepository, new FakeAuditLogRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(now.AddDays(2)), Options.Create(new JwtOptions { RefreshTokenExpirationDays = 1 }));

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => laterService.RotateAsync(issued.RawToken));
    }

    [Fact]
    public async Task RotateAsync_RevokedToken_Throws()
    {
        var (service, _, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);
        await service.RevokeAsync(issued.RawToken, user.Id);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RotateAsync(issued.RawToken));
    }

    [Fact]
    public async Task RotateAsync_DeactivatedUser_Throws()
    {
        var (service, _, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);
        user.IsActive = false;

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RotateAsync(issued.RawToken));
    }

    [Fact]
    public async Task RevokeAsync_Logout_MarksTokenRevoked()
    {
        var (service, tokenRepository, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);

        await service.RevokeAsync(issued.RawToken, user.Id);

        var stored = tokenRepository.Tokens.Single(t => t.Id == issued.TokenId);
        Assert.NotNull(stored.RevokedAt);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task RevokeAsync_ForADifferentUser_Throws()
    {
        var (service, _, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RevokeAsync(issued.RawToken, Guid.NewGuid()));
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevokedToken_Throws()
    {
        var (service, _, userRepository) = CreateService(DateTimeOffset.UtcNow);
        var user = ActiveUser();
        userRepository.Users.Add(user);
        var issued = await service.IssueAsync(user.Id);
        await service.RevokeAsync(issued.RawToken, user.Id);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => service.RevokeAsync(issued.RawToken, user.Id));
    }
}
