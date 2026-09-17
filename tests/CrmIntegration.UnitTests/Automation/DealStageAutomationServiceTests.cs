using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.Automation;

public class DealStageAutomationServiceTests
{
    private static (DealStageAutomationService Service, FakeDealStageTransitionRepository TransitionRepository, FakeAutomationExecutor AutomationExecutor, FakeOnboardingRepository OnboardingRepository, FakeDealRepository DealRepository)
        CreateService()
    {
        var transitionRepository = new FakeDealStageTransitionRepository();
        var automationExecutor = new FakeAutomationExecutor();
        var dealRepository = new FakeDealRepository();
        var onboardingRepository = new FakeOnboardingRepository();
        var onboardingService = new OnboardingService(dealRepository, onboardingRepository, new FakeUnitOfWork(), new FixedTimeProvider(DateTimeOffset.UtcNow), NullLogger<OnboardingService>.Instance);

        var service = new DealStageAutomationService(
            transitionRepository, automationExecutor, onboardingService, new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<DealStageAutomationService>.Instance);

        // FakeAutomationExecutor actually invokes the automation action (unlike a pure no-op
        // spy), so ClosedWon-triggering tests must add their Deal to this same repository for
        // OnboardingService.HandleDealClosedWonAsync to find it.
        return (service, transitionRepository, automationExecutor, onboardingRepository, dealRepository);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenStageUnchanged()
    {
        var (service, transitionRepository, automationExecutor, _, _) = CreateService();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", Stage = DealStage.Proposal, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var result = await service.EvaluateAsync(deal, previousStage: DealStage.Proposal, TransitionSource.HubSpotSync, "corr-1");

        Assert.Null(result);
        Assert.Empty(transitionRepository.Transitions);
        Assert.Empty(automationExecutor.Calls);
    }

    [Fact]
    public async Task EvaluateAsync_RecordsTransition_WhenStageChanges_ButNotToClosedWon()
    {
        var (service, transitionRepository, automationExecutor, _, _) = CreateService();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", Stage = DealStage.Negotiation, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var result = await service.EvaluateAsync(deal, previousStage: DealStage.Proposal, TransitionSource.InternalUpdate, "corr-1");

        Assert.NotNull(result);
        Assert.Equal(DealStage.Proposal, result!.FromStage);
        Assert.Equal(DealStage.Negotiation, result.ToStage);
        Assert.Single(transitionRepository.Transitions);
        Assert.Empty(automationExecutor.Calls);
    }

    [Fact]
    public async Task EvaluateAsync_RecordsTransitionWithNullFromStage_WhenDealIsNew()
    {
        var (service, transitionRepository, _, _, _) = CreateService();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", Stage = DealStage.QualifiedToBuy, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var result = await service.EvaluateAsync(deal, previousStage: null, TransitionSource.InternalUpdate, "corr-1");

        Assert.NotNull(result);
        Assert.Null(result!.FromStage);
        Assert.Single(transitionRepository.Transitions);
    }

    [Fact]
    public async Task EvaluateAsync_TriggersAutomationExecutor_WhenStageBecomesClosedWon()
    {
        var (service, _, automationExecutor, onboardingRepository, dealRepository) = CreateService();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", CompanyId = Guid.NewGuid(), Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);

        var result = await service.EvaluateAsync(deal, previousStage: DealStage.Negotiation, TransitionSource.HubSpotWebhook, "corr-1");

        Assert.NotNull(result);
        var call = Assert.Single(automationExecutor.Calls);
        Assert.Equal(AutomationType.DealClosedWonOnboarding, call.AutomationType);
        Assert.Equal(EntityType.Deal, call.EntityType);
        Assert.Equal(deal.Id, call.EntityId);
        Assert.Equal($"DealClosedWonOnboarding:{result!.Id}", call.IdempotencyKey);
        Assert.Single(onboardingRepository.Records);
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotTriggerAutomationExecutor_WhenStageChangesToClosedLost()
    {
        var (service, _, automationExecutor, _, _) = CreateService();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", Stage = DealStage.ClosedLost, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        await service.EvaluateAsync(deal, previousStage: DealStage.Negotiation, TransitionSource.HubSpotWebhook, "corr-1");

        Assert.Empty(automationExecutor.Calls);
    }

    [Fact]
    public async Task EvaluateAsync_ReenteringClosedWon_ProducesFreshIdempotencyKeyPerTransition()
    {
        var (service, _, automationExecutor, onboardingRepository, dealRepository) = CreateService();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", CompanyId = Guid.NewGuid(), Stage = DealStage.ClosedWon, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        dealRepository.Deals.Add(deal);

        var first = await service.EvaluateAsync(deal, previousStage: DealStage.Negotiation, TransitionSource.HubSpotWebhook, "corr-1");
        deal.Stage = DealStage.Negotiation;
        await service.EvaluateAsync(deal, previousStage: DealStage.ClosedWon, TransitionSource.HubSpotWebhook, "corr-2");
        deal.Stage = DealStage.ClosedWon;
        var third = await service.EvaluateAsync(deal, previousStage: DealStage.Negotiation, TransitionSource.HubSpotWebhook, "corr-3");

        Assert.Equal(2, automationExecutor.Calls.Count(c => c.AutomationType == AutomationType.DealClosedWonOnboarding));
        Assert.NotEqual(first!.Id, third!.Id);
        Assert.All(automationExecutor.Calls, c => Assert.Contains(c.IdempotencyKey, new[]
        {
            $"DealClosedWonOnboarding:{first.Id}",
            $"DealClosedWonOnboarding:{third.Id}"
        }));
        // Re-entering Closed Won must still reuse the single onboarding record for this Deal (the
        // DealId unique index / OnboardingService's own check), not fabricate a second one.
        Assert.Single(onboardingRepository.Records);
    }
}
