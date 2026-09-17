using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class ActivityReportTests
{
    [Fact]
    public async Task GetActivityAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetActivityAsync(50);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetActivityAsync_OrdersAllSourcesByMostRecentFirst()
    {
        var repo = new FakeSalesReportingRepository();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal A", Stage = DealStage.ClosedWon, Status = DealStatus.Won, Amount = 100m, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var contact = new Contact { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = "a@b.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        repo.Deals.Add(deal);
        repo.Contacts.Add(contact);

        var t0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        repo.DealStageTransitions.Add(new DealStageTransition { Id = Guid.NewGuid(), DealId = deal.Id, FromStage = DealStage.Negotiation, ToStage = DealStage.ClosedWon, OccurredAt = t0, Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = t0 });
        repo.ContactLifecycleTransitions.Add(new ContactLifecycleTransition { Id = Guid.NewGuid(), ContactId = contact.Id, FromStage = LifecycleStage.Lead, ToStage = LifecycleStage.Customer, OccurredAt = t0.AddMinutes(5), Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = t0.AddMinutes(5) });
        repo.OnboardingRecords.Add(new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal.Id, CompanyId = Guid.NewGuid(), Status = OnboardingStatus.Pending, TriggerSource = "x", CreatedAt = t0.AddMinutes(10), UpdatedAt = t0.AddMinutes(10) });
        repo.AutomationExecutions.Add(new AutomationExecution { Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal, EntityId = deal.Id, IdempotencyKey = "k", Status = AutomationStatus.Succeeded, CorrelationId = "c", StartedAt = t0.AddMinutes(9), CompletedAt = t0.AddMinutes(9) });

        var service = new SalesReportingService(repo);

        var result = await service.GetActivityAsync(50);

        Assert.Equal(4, result.Items.Count);
        // Most recent first: onboarding (t0+10) > automation (t0+9) > lifecycle (t0+5) > stage transition (t0)
        Assert.Equal(SalesActivityType.OnboardingCreated, result.Items[0].Type);
        Assert.Equal(SalesActivityType.AutomationCompleted, result.Items[1].Type);
        Assert.Equal(SalesActivityType.ContactLifecycleChanged, result.Items[2].Type);
        Assert.Equal(SalesActivityType.DealClosedWon, result.Items[3].Type);
        Assert.True(result.Items.Zip(result.Items.Skip(1)).All(pair => pair.First.OccurredAt >= pair.Second.OccurredAt));
    }

    [Fact]
    public async Task GetActivityAsync_RespectsLimit()
    {
        var repo = new FakeSalesReportingRepository();
        for (var i = 0; i < 5; i++)
        {
            var deal = new Deal { Id = Guid.NewGuid(), Name = $"Deal {i}", Stage = DealStage.Proposal, Status = DealStatus.Open, Amount = 100m, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            repo.Deals.Add(deal);
            repo.DealStageTransitions.Add(new DealStageTransition { Id = Guid.NewGuid(), DealId = deal.Id, FromStage = null, ToStage = DealStage.Proposal, OccurredAt = DateTime.UtcNow.AddMinutes(i), Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = DateTime.UtcNow });
        }
        var service = new SalesReportingService(repo);

        var result = await service.GetActivityAsync(2);

        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetActivityAsync_NormalizesDealClosedWon_DistinctFromGenericStageChange()
    {
        var repo = new FakeSalesReportingRepository();
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Deal", Stage = DealStage.Proposal, Status = DealStatus.Open, Amount = 100m, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        repo.Deals.Add(deal);
        repo.DealStageTransitions.Add(new DealStageTransition { Id = Guid.NewGuid(), DealId = deal.Id, FromStage = DealStage.QualifiedToBuy, ToStage = DealStage.Proposal, OccurredAt = DateTime.UtcNow, Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = DateTime.UtcNow });
        var service = new SalesReportingService(repo);

        var result = await service.GetActivityAsync(50);

        var item = Assert.Single(result.Items);
        Assert.Equal(SalesActivityType.DealStageChanged, item.Type);
        Assert.Contains("stage changed", item.Description);
    }
}
