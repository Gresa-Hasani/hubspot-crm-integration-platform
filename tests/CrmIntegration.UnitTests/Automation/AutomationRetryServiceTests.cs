using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.Automation;

public class AutomationRetryServiceTests
{
    private static (AutomationRetryService Service, FakeAutomationExecutionRepository ExecutionRepository, FakeDealRepository DealRepository, FakeOnboardingRepository OnboardingRepository)
        CreateService()
    {
        var executionRepository = new FakeAutomationExecutionRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var automationExecutor = new AutomationExecutor(executionRepository, auditLogRepository, unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), NullLogger<AutomationExecutor>.Instance);

        var dealRepository = new FakeDealRepository();
        var onboardingRepository = new FakeOnboardingRepository();
        var onboardingService = new OnboardingService(dealRepository, onboardingRepository, unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), NullLogger<OnboardingService>.Instance);

        var service = new AutomationRetryService(executionRepository, automationExecutor, onboardingService);
        return (service, executionRepository, dealRepository, onboardingRepository);
    }

    [Fact]
    public async Task RetryAsync_Throws_WhenExecutionDoesNotExist()
    {
        var (service, _, _, _) = CreateService();

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.RetryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RetryAsync_Throws_WhenExecutionIsNotFailed()
    {
        var (service, executionRepository, _, _) = CreateService();
        var execution = new AutomationExecution
        {
            Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal,
            EntityId = Guid.NewGuid(), IdempotencyKey = "key-1", Status = AutomationStatus.Succeeded,
            CorrelationId = "corr-1", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow
        };
        executionRepository.Executions.Add(execution);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.RetryAsync(execution.Id));
    }

    [Fact]
    public async Task RetryAsync_Throws_ForUnsupportedAutomationType()
    {
        var (service, executionRepository, _, _) = CreateService();
        var execution = new AutomationExecution
        {
            Id = Guid.NewGuid(), AutomationType = AutomationType.ContactLifecycleTransition, EntityType = EntityType.Contact,
            EntityId = Guid.NewGuid(), IdempotencyKey = "key-2", Status = AutomationStatus.Failed,
            CorrelationId = "corr-1", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow
        };
        executionRepository.Executions.Add(execution);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.RetryAsync(execution.Id));
    }

    [Fact]
    public async Task RetryAsync_ReRunsHandleDealClosedWonAsync_WithFreshIdempotencyKey_ForFailedExecution()
    {
        var (service, executionRepository, dealRepository, onboardingRepository) = CreateService();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", CompanyId = Guid.NewGuid(), Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);

        var failedExecution = new AutomationExecution
        {
            Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal,
            EntityId = deal.Id, IdempotencyKey = "DealClosedWonOnboarding:original-transition", Status = AutomationStatus.Failed,
            CorrelationId = "corr-1", StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow,
            FailureCategory = "SomeTransientError", ErrorMessage = "boom"
        };
        executionRepository.Executions.Add(failedExecution);

        var retried = await service.RetryAsync(failedExecution.Id);

        Assert.Equal(AutomationStatus.Succeeded, retried.Status);
        Assert.NotEqual(failedExecution.Id, retried.Id);
        Assert.StartsWith(failedExecution.IdempotencyKey + ":retry:", retried.IdempotencyKey);
        Assert.Single(onboardingRepository.Records);
        Assert.Equal(2, executionRepository.Executions.Count); // original Failed row preserved + new retry row
    }
}
