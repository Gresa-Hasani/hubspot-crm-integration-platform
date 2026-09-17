using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class SalesOverviewReportTests
{
    private static Deal NewDeal(DealStage stage, DealStatus status, decimal amount, string currency = "EUR") => new()
    {
        Id = Guid.NewGuid(), Name = "Deal " + Guid.NewGuid().ToString("N")[..6], Stage = stage, Status = status,
        Amount = amount, Currency = currency, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetOverviewAsync_EmptyDatabase_ReturnsAllZeroesAndSafeRates()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetOverviewAsync();

        Assert.Equal(0, result.TotalContacts);
        Assert.Equal(0, result.TotalCompanies);
        Assert.Equal(0, result.TotalDeals);
        Assert.Equal(0, result.OpenDeals);
        Assert.Equal(0, result.WonDeals);
        Assert.Equal(0, result.LostDeals);
        Assert.Empty(result.OpenPipelineValue);
        Assert.Empty(result.WonRevenue);
        Assert.Empty(result.AverageDealSize);
        Assert.Equal(0m, result.WinRate); // zero denominator handled safely, not NaN/exception
        Assert.Equal(0, result.OnboardingCount);
    }

    [Fact]
    public async Task GetOverviewAsync_CountsDealsByStatus_AndComputesPipelineAndRevenue()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 10000m));
        repo.Deals.Add(NewDeal(DealStage.Negotiation, DealStatus.Open, 5000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 20000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 30000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedLost, DealStatus.Lost, 8000m));
        var service = new SalesReportingService(repo);

        var result = await service.GetOverviewAsync();

        Assert.Equal(5, result.TotalDeals);
        Assert.Equal(2, result.OpenDeals);
        Assert.Equal(2, result.WonDeals);
        Assert.Equal(1, result.LostDeals);
        Assert.Equal(15000m, Assert.Single(result.OpenPipelineValue).Amount);
        Assert.Equal(50000m, Assert.Single(result.WonRevenue).Amount);
        // average deal size across ALL 5 deals: (10000+5000+20000+30000+8000)/5 = 14600
        Assert.Equal(14600m, Assert.Single(result.AverageDealSize).Amount);
        // win rate = won / (won+lost) = 2/3 -> 66.67%
        Assert.Equal(66.67m, result.WinRate);
    }

    [Fact]
    public async Task GetOverviewAsync_WinRate_IsZero_WhenOnlyOpenDealsExist()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 1000m));
        var service = new SalesReportingService(repo);

        var result = await service.GetOverviewAsync();

        Assert.Equal(0m, result.WinRate);
    }

    [Fact]
    public async Task GetOverviewAsync_GroupsMonetaryTotalsByCurrency_NeverSummingAcrossCurrencies()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 1000m, "EUR"));
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 500m, "USD"));
        var service = new SalesReportingService(repo);

        var result = await service.GetOverviewAsync();

        Assert.Equal(2, result.OpenPipelineValue.Count);
        Assert.Contains(result.OpenPipelineValue, m => m.Currency == "EUR" && m.Amount == 1000m);
        Assert.Contains(result.OpenPipelineValue, m => m.Currency == "USD" && m.Amount == 500m);
    }

    [Fact]
    public async Task GetOverviewAsync_ReportsContactCompanyAndOnboardingCounts()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Contacts.Add(new Contact { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = "a@b.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        repo.Companies.Add(new Company { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        repo.OnboardingRecords.Add(new OnboardingRecord { Id = Guid.NewGuid(), DealId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), TriggerSource = "x", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var service = new SalesReportingService(repo);

        var result = await service.GetOverviewAsync();

        Assert.Equal(1, result.TotalContacts);
        Assert.Equal(1, result.TotalCompanies);
        Assert.Equal(1, result.OnboardingCount);
    }
}
