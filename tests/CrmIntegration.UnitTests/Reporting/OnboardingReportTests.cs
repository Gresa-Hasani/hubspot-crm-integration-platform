using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class OnboardingReportTests
{
    [Fact]
    public async Task GetOnboardingAsync_EmptyDatabase_ReturnsZeroTotals()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetOnboardingAsync();

        Assert.Equal(0, result.TotalOnboardingRecords);
        Assert.Empty(result.ByStatus);
        Assert.Empty(result.AssociatedDealValue);
    }

    [Fact]
    public async Task GetOnboardingAsync_GroupsByStatus_AndAggregatesDealValue()
    {
        var repo = new FakeSalesReportingRepository();
        var company = new Company { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var deal1 = new Deal { Id = Guid.NewGuid(), Name = "Deal 1", CompanyId = company.Id, Stage = DealStage.ClosedWon, Status = DealStatus.Won, Amount = 1000m, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var deal2 = new Deal { Id = Guid.NewGuid(), Name = "Deal 2", CompanyId = company.Id, Stage = DealStage.ClosedWon, Status = DealStatus.Won, Amount = 2000m, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        repo.Companies.Add(company);
        repo.Deals.AddRange(new[] { deal1, deal2 });
        repo.OnboardingRecords.Add(new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal1.Id, CompanyId = company.Id, Status = OnboardingStatus.Pending, TriggerSource = "x", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        repo.OnboardingRecords.Add(new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal2.Id, CompanyId = company.Id, Status = OnboardingStatus.Completed, TriggerSource = "x", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var service = new SalesReportingService(repo);

        var result = await service.GetOnboardingAsync();

        Assert.Equal(2, result.TotalOnboardingRecords);
        Assert.Equal(1, result.ByStatus.Single(s => s.Status == OnboardingStatus.Pending).Count);
        Assert.Equal(1, result.ByStatus.Single(s => s.Status == OnboardingStatus.Completed).Count);
        Assert.Equal(3000m, Assert.Single(result.AssociatedDealValue).Amount);
    }

    [Fact]
    public async Task GetOnboardingAsync_RecentHandoffs_IncludeDealAndCompanyInfo()
    {
        var repo = new FakeSalesReportingRepository();
        var company = new Company { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var deal = new Deal { Id = Guid.NewGuid(), Name = "Big Deal", CompanyId = company.Id, Stage = DealStage.ClosedWon, Status = DealStatus.Won, Amount = 5000m, Currency = "EUR", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        repo.Companies.Add(company);
        repo.Deals.Add(deal);
        repo.OnboardingRecords.Add(new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal.Id, CompanyId = company.Id, Status = OnboardingStatus.Pending, TriggerSource = "x", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var service = new SalesReportingService(repo);

        var result = await service.GetOnboardingAsync();

        var handoff = Assert.Single(result.RecentHandoffs);
        Assert.Equal("Big Deal", handoff.DealName);
        Assert.Equal(5000m, handoff.DealAmount);
        Assert.Equal("Acme", handoff.CompanyName);
        Assert.Null(handoff.ContactName); // no Contact on this OnboardingRecord
    }
}
