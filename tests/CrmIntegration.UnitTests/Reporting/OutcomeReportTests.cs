using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class OutcomeReportTests
{
    private static Deal NewDeal(DealStage stage, DealStatus status, decimal amount) => new()
    {
        Id = Guid.NewGuid(), Name = "Deal", Stage = stage, Status = status,
        Amount = amount, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetOutcomesAsync_NoClosedDeals_ReturnsZeroRatesSafely()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 1000m));
        var service = new SalesReportingService(repo);

        var result = await service.GetOutcomesAsync(null, null);

        Assert.Equal(0, result.WonDealCount);
        Assert.Equal(0, result.LostDealCount);
        Assert.Equal(1, result.OpenDealCount);
        Assert.Equal(0m, result.WinRate);
        Assert.Equal(0m, result.LossRate);
    }

    [Fact]
    public async Task GetOutcomesAsync_ComputesWinAndLossRates()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 1000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 2000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 3000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedLost, DealStatus.Lost, 500m));
        var service = new SalesReportingService(repo);

        var result = await service.GetOutcomesAsync(null, null);

        Assert.Equal(3, result.WonDealCount);
        Assert.Equal(1, result.LostDealCount);
        Assert.Equal(75.00m, result.WinRate);
        Assert.Equal(25.00m, result.LossRate);
        Assert.Equal(6000m, Assert.Single(result.TotalWonValue).Amount);
        Assert.Equal(500m, Assert.Single(result.TotalLostValue).Amount);
    }

    [Fact]
    public async Task GetOutcomesAsync_WithoutDateFilter_IncludesDealsLackingTransitionHistory()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 1000m)); // no transition seeded
        var service = new SalesReportingService(repo);

        var result = await service.GetOutcomesAsync(null, null);

        Assert.Equal(1, result.WonDealCount);
        Assert.Equal(0, result.ExcludedDealsWithoutTransitionHistory);
    }

    [Fact]
    public async Task GetOutcomesAsync_WithDateFilter_ExcludesDealsLackingTransitionHistory()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 1000m)); // no transition seeded
        var service = new SalesReportingService(repo);

        var result = await service.GetOutcomesAsync("2026-01-01", "2026-12-31");

        Assert.Equal(0, result.WonDealCount);
        Assert.Equal(1, result.ExcludedDealsWithoutTransitionHistory);
    }
}
