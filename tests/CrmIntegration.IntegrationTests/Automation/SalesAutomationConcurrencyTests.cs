using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Automation;

/// <summary>
/// Verifies the actual database-level idempotency/uniqueness constraints from the Phase 6
/// migration — not just the application-layer logic that relies on them — and that concurrent
/// automation runs for the same Deal never produce more than one OnboardingRecord.
/// </summary>
public class SalesAutomationConcurrencyTests
{
    [Fact]
    public async Task AutomationExecutions_IdempotencyKey_IsRejectedByDatabase_OnDuplicateInsert()
    {
        using var factory = new CrmApiFactory();
        var idempotencyKey = $"dup-key-{Guid.NewGuid():N}";
        var entityId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AutomationExecutions.Add(new AutomationExecution
            {
                Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal,
                EntityId = entityId, IdempotencyKey = idempotencyKey, Status = AutomationStatus.Succeeded,
                CorrelationId = "corr-1", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var duplicateScope = factory.Services.CreateScope();
        var duplicateDb = duplicateScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitOfWork = duplicateScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        duplicateDb.AutomationExecutions.Add(new AutomationExecution
        {
            Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal,
            EntityId = entityId, IdempotencyKey = idempotencyKey, Status = AutomationStatus.Succeeded,
            CorrelationId = "corr-2", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<SyncMappingConflictException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task OnboardingRecords_DealId_IsRejectedByDatabase_OnDuplicateInsert()
    {
        using var factory = new CrmApiFactory();
        var dealId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Companies.Add(new Company { Id = companyId, Name = "Dup Test Co " + Guid.NewGuid().ToString("N")[..8], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.Deals.Add(new Deal { Id = dealId, Name = "Dup Test Deal", CompanyId = companyId, Stage = DealStage.ClosedWon, Status = DealStatus.Won, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.OnboardingRecords.Add(new OnboardingRecord { Id = Guid.NewGuid(), DealId = dealId, CompanyId = companyId, Status = OnboardingStatus.Pending, TriggerSource = "Test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var duplicateScope = factory.Services.CreateScope();
        var duplicateDb = duplicateScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitOfWork = duplicateScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        duplicateDb.OnboardingRecords.Add(new OnboardingRecord { Id = Guid.NewGuid(), DealId = dealId, CompanyId = companyId, Status = OnboardingStatus.Pending, TriggerSource = "Test", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        await Assert.ThrowsAsync<SyncMappingConflictException>(() => unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task ConcurrentClosedWonAutomation_ForSameDeal_ProducesExactlyOneOnboardingRecord()
    {
        using var factory = new CrmApiFactory();
        Guid dealId;
        Guid companyId = Guid.NewGuid();

        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Companies.Add(new Company { Id = companyId, Name = "Concurrency Co " + Guid.NewGuid().ToString("N")[..8], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            var deal = new Deal { Id = Guid.NewGuid(), Name = "Concurrency Deal", CompanyId = companyId, Stage = DealStage.ClosedWon, Status = DealStatus.Won, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.Deals.Add(deal);
            await db.SaveChangesAsync();
            dealId = deal.Id;
        }

        // Each concurrent caller gets its own DI scope (own AppDbContext/DbContext-scoped
        // repositories), mirroring how two concurrent HTTP requests or a sync + a manual retry
        // would each get their own scope in the real app — the only thing enforcing "exactly one
        // OnboardingRecord" across them is the DB's unique index on OnboardingRecords.DealId.
        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var onboardingService = scope.ServiceProvider.GetRequiredService<IOnboardingService>();
            return await onboardingService.HandleDealClosedWonAsync(dealId);
        });

        var outcomes = await Task.WhenAll(tasks);

        Assert.All(outcomes, o => Assert.Equal(AutomationStatus.Succeeded, o.Status));

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var onboardingCount = await verifyDb.OnboardingRecords.CountAsync(o => o.DealId == dealId);
        Assert.Equal(1, onboardingCount);
    }

    [Fact]
    public async Task ConcurrentAutomationExecutor_ForSameIdempotencyKey_ProducesExactlyOneExecutionRow()
    {
        using var factory = new CrmApiFactory();
        var idempotencyKey = $"concurrent-key-{Guid.NewGuid():N}";
        var entityId = Guid.NewGuid();
        var invocationCount = 0;

        var tasks = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var executor = scope.ServiceProvider.GetRequiredService<IAutomationExecutor>();
            return await executor.ExecuteAsync(
                AutomationType.ContactLifecycleTransition, EntityType.Contact, entityId, idempotencyKey, "concurrent-corr",
                _ =>
                {
                    Interlocked.Increment(ref invocationCount);
                    return Task.FromResult(new AutomationOutcome(AutomationStatus.Succeeded, "did work"));
                });
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, invocationCount); // the action must run exactly once across all concurrent callers
        Assert.True(results.Select(r => r.Id).Distinct().Count() == 1); // every caller must observe the same winning row

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var count = await verifyDb.AutomationExecutions.CountAsync(e => e.IdempotencyKey == idempotencyKey);
        Assert.Equal(1, count);
    }
}
