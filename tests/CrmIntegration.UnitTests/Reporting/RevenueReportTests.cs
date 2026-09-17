using CrmIntegration.Application.Common;
using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class RevenueReportTests
{
    private static Deal NewWonDeal(decimal amount, DateTime createdAt, string currency = "EUR") => new()
    {
        Id = Guid.NewGuid(), Name = "Deal", Stage = DealStage.ClosedWon, Status = DealStatus.Won,
        Amount = amount, Currency = currency, CreatedAt = createdAt, UpdatedAt = createdAt
    };

    private static DealStageTransition ClosedWonTransition(Guid dealId, DateTime occurredAt) => new()
    {
        Id = Guid.NewGuid(), DealId = dealId, FromStage = DealStage.Negotiation, ToStage = DealStage.ClosedWon,
        OccurredAt = occurredAt, Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = occurredAt
    };

    [Fact]
    public async Task GetRevenueAsync_UsesDealStageTransitionTimestamp_NotDealCreatedAt()
    {
        var repo = new FakeSalesReportingRepository();
        var deal = NewWonDeal(1000m, createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        repo.Deals.Add(deal);
        // The deal was created in January but only actually reached ClosedWon in March.
        repo.DealStageTransitions.Add(ClosedWonTransition(deal.Id, new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc)));
        var service = new SalesReportingService(repo);

        var result = await service.GetRevenueAsync(from: "2026-03-01", to: "2026-03-31", groupBy: "day");

        Assert.Equal(1, result.WonDealCount); // found via the March transition date, not the January CreatedAt
        Assert.Equal(0, result.ExcludedWonDealsWithoutTransitionHistory);
    }

    [Fact]
    public async Task GetRevenueAsync_ExcludesWonDealsWithNoTransitionHistory_AndCountsThem()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewWonDeal(1000m, DateTime.UtcNow)); // no DealStageTransition seeded
        var service = new SalesReportingService(repo);

        var result = await service.GetRevenueAsync(from: null, to: null, groupBy: null);

        Assert.Equal(0, result.WonDealCount);
        Assert.Equal(1, result.ExcludedWonDealsWithoutTransitionHistory);
        Assert.Empty(result.TotalWonRevenue);
    }

    [Fact]
    public async Task GetRevenueAsync_FiltersByDateRange_Inclusive()
    {
        var repo = new FakeSalesReportingRepository();
        var inRange = NewWonDeal(100m, DateTime.UtcNow);
        var beforeRange = NewWonDeal(200m, DateTime.UtcNow);
        var afterRange = NewWonDeal(300m, DateTime.UtcNow);
        repo.Deals.AddRange(new[] { inRange, beforeRange, afterRange });
        repo.DealStageTransitions.Add(ClosedWonTransition(inRange.Id, new DateTime(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc)));
        repo.DealStageTransitions.Add(ClosedWonTransition(beforeRange.Id, new DateTime(2026, 1, 31, 23, 59, 0, DateTimeKind.Utc)));
        repo.DealStageTransitions.Add(ClosedWonTransition(afterRange.Id, new DateTime(2026, 3, 1, 0, 0, 1, DateTimeKind.Utc)));
        var service = new SalesReportingService(repo);

        var result = await service.GetRevenueAsync(from: "2026-02-01", to: "2026-02-28", groupBy: "day");

        Assert.Equal(1, result.WonDealCount);
        Assert.Equal(100m, Assert.Single(result.TotalWonRevenue).Amount);
    }

    [Fact]
    public async Task GetRevenueAsync_BoundaryDates_AreInclusive()
    {
        var repo = new FakeSalesReportingRepository();
        var onFromBoundary = NewWonDeal(50m, DateTime.UtcNow);
        var onToBoundary = NewWonDeal(75m, DateTime.UtcNow);
        repo.Deals.AddRange(new[] { onFromBoundary, onToBoundary });
        repo.DealStageTransitions.Add(ClosedWonTransition(onFromBoundary.Id, new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)));
        repo.DealStageTransitions.Add(ClosedWonTransition(onToBoundary.Id, new DateTime(2026, 5, 31, 23, 59, 59, 999, DateTimeKind.Utc)));
        var service = new SalesReportingService(repo);

        var result = await service.GetRevenueAsync(from: "2026-05-01", to: "2026-05-31", groupBy: "day");

        Assert.Equal(2, result.WonDealCount);
    }

    [Fact]
    public async Task GetRevenueAsync_EmptyRange_ReturnsZeroesNotError()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetRevenueAsync(from: "2026-01-01", to: "2026-01-31", groupBy: "day");

        Assert.Equal(0, result.WonDealCount);
        Assert.Empty(result.TotalWonRevenue);
        Assert.Empty(result.Buckets);
    }

    [Fact]
    public async Task GetRevenueAsync_GroupsByDay_WhenRequested()
    {
        var repo = new FakeSalesReportingRepository();
        var d1 = NewWonDeal(100m, DateTime.UtcNow);
        var d2 = NewWonDeal(200m, DateTime.UtcNow);
        repo.Deals.AddRange(new[] { d1, d2 });
        repo.DealStageTransitions.Add(ClosedWonTransition(d1.Id, new DateTime(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc)));
        repo.DealStageTransitions.Add(ClosedWonTransition(d2.Id, new DateTime(2026, 4, 10, 15, 0, 0, DateTimeKind.Utc)));
        var service = new SalesReportingService(repo);

        var result = await service.GetRevenueAsync(from: null, to: null, groupBy: "day");

        var bucket = Assert.Single(result.Buckets);
        Assert.Equal(new DateOnly(2026, 4, 10), bucket.BucketStart);
        Assert.Equal(300m, bucket.TotalAmount);
        Assert.Equal(2, bucket.DealCount);
    }

    [Fact]
    public async Task GetRevenueAsync_GroupsByMonth_ByDefault()
    {
        var repo = new FakeSalesReportingRepository();
        var d1 = NewWonDeal(100m, DateTime.UtcNow);
        var d2 = NewWonDeal(200m, DateTime.UtcNow);
        repo.Deals.AddRange(new[] { d1, d2 });
        repo.DealStageTransitions.Add(ClosedWonTransition(d1.Id, new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc)));
        repo.DealStageTransitions.Add(ClosedWonTransition(d2.Id, new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc)));
        var service = new SalesReportingService(repo);

        var result = await service.GetRevenueAsync(from: null, to: null, groupBy: null);

        Assert.Equal(RevenueGroupBy.Month, result.GroupBy);
        var bucket = Assert.Single(result.Buckets);
        Assert.Equal(new DateOnly(2026, 4, 1), bucket.BucketStart);
        Assert.Equal(300m, bucket.TotalAmount);
    }

    [Fact]
    public async Task GetRevenueAsync_InvalidGroupBy_ThrowsDomainValidationException()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.GetRevenueAsync(null, null, "week"));
    }

    [Fact]
    public async Task GetRevenueAsync_InvalidDateFormat_ThrowsDomainValidationException()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.GetRevenueAsync("not-a-date", null, null));
    }

    [Fact]
    public async Task GetRevenueAsync_FromAfterTo_ThrowsDomainValidationException()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.GetRevenueAsync("2026-05-01", "2026-01-01", null));
    }
}
