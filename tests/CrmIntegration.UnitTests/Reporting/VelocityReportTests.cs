using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class VelocityReportTests
{
    private static Deal NewDeal(DealStage stage, DealStatus status, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(), Name = "Deal", Stage = stage, Status = status,
        Amount = 1000m, Currency = "EUR", CreatedAt = createdAt, UpdatedAt = createdAt
    };

    private static DealStageTransition Transition(Guid dealId, DealStage? from, DealStage to, DateTime occurredAt) => new()
    {
        Id = Guid.NewGuid(), DealId = dealId, FromStage = from, ToStage = to,
        OccurredAt = occurredAt, Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = occurredAt
    };

    [Fact]
    public async Task GetVelocityAsync_NoTransitionHistory_ReturnsNullAveragesAndExcludesDeals()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, DateTime.UtcNow)); // no transition seeded
        var service = new SalesReportingService(repo);

        var result = await service.GetVelocityAsync();

        Assert.Equal(0, result.WonDealsWithTransitionHistory);
        Assert.Null(result.AverageDaysToClosedWon);
        Assert.Null(result.MedianDaysToClosedWon);
        Assert.Equal(1, result.DealsExcludedWithoutTransitionHistory);
        Assert.Empty(result.RecentClosedWonCycleTimes);
    }

    [Fact]
    public async Task GetVelocityAsync_ComputesAverageAndMedianCreationToClosedWon()
    {
        var repo = new FakeSalesReportingRepository();
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var d1 = NewDeal(DealStage.ClosedWon, DealStatus.Won, created);
        var d2 = NewDeal(DealStage.ClosedWon, DealStatus.Won, created);
        var d3 = NewDeal(DealStage.ClosedWon, DealStatus.Won, created);
        repo.Deals.AddRange(new[] { d1, d2, d3 });
        // Durations: 10 days, 20 days, 30 days -> average 20, median 20.
        repo.DealStageTransitions.Add(Transition(d1.Id, DealStage.Negotiation, DealStage.ClosedWon, created.AddDays(10)));
        repo.DealStageTransitions.Add(Transition(d2.Id, DealStage.Negotiation, DealStage.ClosedWon, created.AddDays(20)));
        repo.DealStageTransitions.Add(Transition(d3.Id, DealStage.Negotiation, DealStage.ClosedWon, created.AddDays(30)));
        var service = new SalesReportingService(repo);

        var result = await service.GetVelocityAsync();

        Assert.Equal(3, result.WonDealsWithTransitionHistory);
        Assert.Equal(20.0, result.AverageDaysToClosedWon);
        Assert.Equal(20.0, result.MedianDaysToClosedWon);
    }

    [Fact]
    public async Task GetVelocityAsync_ComputesAverageCreationToClosedLost()
    {
        var repo = new FakeSalesReportingRepository();
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var lost = NewDeal(DealStage.ClosedLost, DealStatus.Lost, created);
        repo.Deals.Add(lost);
        repo.DealStageTransitions.Add(Transition(lost.Id, DealStage.Proposal, DealStage.ClosedLost, created.AddDays(5)));
        var service = new SalesReportingService(repo);

        var result = await service.GetVelocityAsync();

        Assert.Equal(1, result.LostDealsWithTransitionHistory);
        Assert.Equal(5.0, result.AverageDaysToClosedLost);
    }

    [Fact]
    public async Task GetVelocityAsync_AverageTimePerStage_OnlyUsesClosedIntervals_ExcludesCurrentStage()
    {
        var repo = new FakeSalesReportingRepository();
        var dealId = Guid.NewGuid();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // Deal enters QualifiedToBuy at t0 (create), Proposal at t0+2d, Negotiation at t0+5d (still there — no exit yet).
        repo.DealStageTransitions.Add(Transition(dealId, null, DealStage.QualifiedToBuy, t0));
        repo.DealStageTransitions.Add(Transition(dealId, DealStage.QualifiedToBuy, DealStage.Proposal, t0.AddDays(2)));
        repo.DealStageTransitions.Add(Transition(dealId, DealStage.Proposal, DealStage.Negotiation, t0.AddDays(5)));
        var service = new SalesReportingService(repo);

        var result = await service.GetVelocityAsync();

        // QualifiedToBuy was occupied for 2 days (closed interval: t0 -> t0+2d).
        var qtb = Assert.Single(result.AverageTimePerStage, m => m.Stage == DealStage.QualifiedToBuy);
        Assert.Equal(2.0, qtb.AverageDurationDays);
        // Proposal was occupied for 3 days (t0+2d -> t0+5d).
        var proposal = Assert.Single(result.AverageTimePerStage, m => m.Stage == DealStage.Proposal);
        Assert.Equal(3.0, proposal.AverageDurationDays);
        // Negotiation is the CURRENT stage (no exit transition yet) — must not appear at all.
        Assert.DoesNotContain(result.AverageTimePerStage, m => m.Stage == DealStage.Negotiation);
    }

    [Fact]
    public async Task GetVelocityAsync_RecentClosedWonCycleTimes_OrderedMostRecentFirst()
    {
        var repo = new FakeSalesReportingRepository();
        var created = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var older = NewDeal(DealStage.ClosedWon, DealStatus.Won, created);
        var newer = NewDeal(DealStage.ClosedWon, DealStatus.Won, created);
        repo.Deals.AddRange(new[] { older, newer });
        repo.DealStageTransitions.Add(Transition(older.Id, DealStage.Negotiation, DealStage.ClosedWon, created.AddDays(10)));
        repo.DealStageTransitions.Add(Transition(newer.Id, DealStage.Negotiation, DealStage.ClosedWon, created.AddDays(20)));
        var service = new SalesReportingService(repo);

        var result = await service.GetVelocityAsync();

        Assert.Equal(2, result.RecentClosedWonCycleTimes.Count);
        Assert.Equal(newer.Id, result.RecentClosedWonCycleTimes[0].DealId);
        Assert.Equal(older.Id, result.RecentClosedWonCycleTimes[1].DealId);
    }
}
