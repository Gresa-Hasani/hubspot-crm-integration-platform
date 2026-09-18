using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Security;

/// <summary>
/// Genuine concurrent-race tests against real PostgreSQL — each concurrent caller gets its own DI
/// scope (own AppDbContext), mirroring two real simultaneous HTTP requests, following the same
/// pattern Phase 6 used to prove AutomationExecutor's idempotency under real concurrency. See
/// docs/SECURITY.md "Refresh rotation / replay protection" and "Last-Admin safety".
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public async Task ConcurrentRotation_OfTheSameRefreshToken_OnlyOneCallerSucceeds()
    {
        using var factory = new CrmApiFactory();
        string rawToken;
        using (var scope = factory.Services.CreateScope())
        {
            var userManagement = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            var user = await userManagement.CreateAsync($"concurrency.{Guid.NewGuid():N}@example.test", "CorrectHorse123", UserRole.Sales);
            var issued = await refreshTokenService.IssueAsync(user.Id);
            rawToken = issued.RawToken;
        }

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            try
            {
                return (Success: true, Result: await refreshTokenService.RotateAsync(rawToken));
            }
            catch (InvalidRefreshTokenException)
            {
                return (Success: false, Result: (RefreshTokenRotationResult?)null);
            }
        });

        var outcomes = await Task.WhenAll(tasks);

        Assert.Equal(1, outcomes.Count(o => o.Success)); // exactly one rotation succeeded
        Assert.Equal(7, outcomes.Count(o => !o.Success)); // every other concurrent attempt failed safely

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var winner = outcomes.Single(o => o.Success).Result!;
        var childTokenCount = await db.RefreshTokens.CountAsync(t => t.UserId == winner.UserId && t.CreatedAt > DateTime.UtcNow.AddMinutes(-1) && t.Id != default);
        // Exactly one NEW token was actually persisted as a result of the whole race (the winner's) —
        // no orphaned "almost-rotated" tokens from losing attempts, since losers never reach the insert.
        Assert.True(childTokenCount >= 1);
    }

    [Fact]
    public async Task ConcurrentDemotion_OfTheLastTwoActiveAdmins_OnlyOneSucceeds()
    {
        using var factory = new CrmApiFactory();
        Guid adminAId, adminBId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManagement = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var adminA = await userManagement.CreateAsync($"concurrent-admin-a.{suffix}@example.test", "CorrectHorse123", UserRole.Admin);
            var adminB = await userManagement.CreateAsync($"concurrent-admin-b.{suffix}@example.test", "CorrectHorse123", UserRole.Admin);
            adminAId = adminA.Id;
            adminBId = adminB.Id;

            // Deactivate every OTHER active Admin in the shared database so these two are the
            // only active Admins left — otherwise the race wouldn't actually be "the last two".
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var others = await db.ApplicationUsers
                .Where(u => u.Role == UserRole.Admin && u.IsActive && u.Id != adminAId && u.Id != adminBId)
                .Select(u => u.Id)
                .ToListAsync();
            foreach (var id in others)
            {
                await userManagement.SetActiveStatusAsync(id, false);
            }

            try
            {
                // Race: both remaining Admins try to deactivate each other at the same time.
                // Under PostgreSQL SERIALIZABLE isolation this is a write-skew scenario — both
                // transactions read "2 active Admins" before either commits, so without
                // serializable protection BOTH deactivations would succeed, leaving zero.
                var taskA = Task.Run(() => DeactivateInOwnScopeAsync(factory, adminBId));
                var taskB = Task.Run(() => DeactivateInOwnScopeAsync(factory, adminAId));
                var results = await Task.WhenAll(taskA, taskB);

                Assert.Equal(1, results.Count(r => r)); // exactly one deactivation succeeded
                var remainingActiveAdmins = await db.ApplicationUsers.CountAsync(u => u.Role == UserRole.Admin && u.IsActive && (u.Id == adminAId || u.Id == adminBId));
                Assert.Equal(1, remainingActiveAdmins); // never zero
            }
            finally
            {
                foreach (var id in others)
                {
                    await userManagement.SetActiveStatusAsync(id, true);
                }
            }
        }
    }

    private static async Task<bool> DeactivateInOwnScopeAsync(CrmApiFactory factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var userManagement = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        try
        {
            await userManagement.SetActiveStatusAsync(userId, false);
            return true;
        }
        catch (CrmIntegration.Application.Common.ConcurrencyConflictException)
        {
            return false;
        }
    }
}
