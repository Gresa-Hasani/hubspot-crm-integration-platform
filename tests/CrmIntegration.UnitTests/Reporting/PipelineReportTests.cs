using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class PipelineReportTests
{
    private static Deal NewDeal(DealStage stage, DealStatus status, decimal amount, string currency = "EUR") => new()
    {
        Id = Guid.NewGuid(), Name = "Deal", Stage = stage, Status = status,
        Amount = amount, Currency = currency, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetPipelineAsync_EmptyDatabase_ReturnsEmptyStages()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetPipelineAsync();

        Assert.Empty(result.Stages);
    }

    [Fact]
    public async Task GetPipelineAsync_ComputesCountAndTotalAndAveragePerStage()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 1000m));
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 3000m));
        var service = new SalesReportingService(repo);

        var result = await service.GetPipelineAsync();

        var proposal = Assert.Single(result.Stages);
        Assert.Equal(2, proposal.DealCount);
        Assert.Equal(4000m, proposal.TotalAmount);
        Assert.Equal(2000m, proposal.AverageAmount);
    }

    [Fact]
    public async Task GetPipelineAsync_PercentageOfOpenPipeline_ExcludesClosedWonAndClosedLost()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 3000m));
        repo.Deals.Add(NewDeal(DealStage.Negotiation, DealStatus.Open, 1000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedWon, DealStatus.Won, 50000m));
        repo.Deals.Add(NewDeal(DealStage.ClosedLost, DealStatus.Lost, 20000m));
        var service = new SalesReportingService(repo);

        var result = await service.GetPipelineAsync();

        var proposal = result.Stages.Single(s => s.Stage == DealStage.Proposal);
        var negotiation = result.Stages.Single(s => s.Stage == DealStage.Negotiation);
        var closedWon = result.Stages.Single(s => s.Stage == DealStage.ClosedWon);
        var closedLost = result.Stages.Single(s => s.Stage == DealStage.ClosedLost);

        // Open pipeline total = 3000 + 1000 = 4000. Proposal = 75%, Negotiation = 25%.
        Assert.Equal(75.00m, proposal.PercentageOfOpenPipeline);
        Assert.Equal(25.00m, negotiation.PercentageOfOpenPipeline);
        Assert.Null(closedWon.PercentageOfOpenPipeline);
        Assert.Null(closedLost.PercentageOfOpenPipeline);
    }

    [Fact]
    public async Task GetPipelineAsync_KeepsCurrenciesSeparate()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 1000m, "EUR"));
        repo.Deals.Add(NewDeal(DealStage.Proposal, DealStatus.Open, 500m, "USD"));
        var service = new SalesReportingService(repo);

        var result = await service.GetPipelineAsync();

        Assert.Equal(2, result.Stages.Count);
        Assert.Contains(result.Stages, s => s.Currency == "EUR" && s.TotalAmount == 1000m);
        Assert.Contains(result.Stages, s => s.Currency == "USD" && s.TotalAmount == 500m);
    }
}
