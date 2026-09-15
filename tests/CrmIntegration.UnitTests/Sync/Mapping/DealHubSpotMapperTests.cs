using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Sync.Mapping;

public class DealHubSpotMapperTests
{
    private static DealHubSpotMapper CreateMapper(HubSpotOptions? options = null)
    {
        options ??= new HubSpotOptions();
        return new DealHubSpotMapper(new HubSpotStageMapper(Options.Create(options)), Options.Create(options));
    }

    [Fact]
    public void ToHubSpotProperties_MapsFieldsAndUsesStageMapper()
    {
        var mapper = CreateMapper();
        var deal = new Deal { Name = "Enterprise Deal", Amount = 25000m, Stage = DealStage.ClosedWon, Currency = "EUR" };

        var properties = mapper.ToHubSpotProperties(deal);

        Assert.Equal("Enterprise Deal", properties["dealname"]);
        Assert.Equal("25000", properties["amount"]);
        Assert.Equal("closedwon", properties["dealstage"]);
    }

    [Fact]
    public void ToHubSpotProperties_OmitsCurrencyCode_ByDefault()
    {
        // Most HubSpot portals don't have multi-currency enabled and reject deal_currency_code
        // outright (discovered during Phase 4 live verification) — so it must be opt-in.
        var mapper = CreateMapper();
        var deal = new Deal { Name = "Enterprise Deal", Amount = 25000m, Currency = "EUR" };

        var properties = mapper.ToHubSpotProperties(deal);

        Assert.False(properties.ContainsKey("deal_currency_code"));
    }

    [Fact]
    public void ToHubSpotProperties_IncludesCurrencyCode_WhenExplicitlyEnabled()
    {
        var mapper = CreateMapper(new HubSpotOptions { SyncDealCurrencyCode = true });
        var deal = new Deal { Name = "Enterprise Deal", Amount = 25000m, Currency = "EUR" };

        var properties = mapper.ToHubSpotProperties(deal);

        Assert.Equal("EUR", properties["deal_currency_code"]);
    }

    [Fact]
    public void ApplyHubSpotProperties_SetsStageAndDerivesStatus()
    {
        var mapper = CreateMapper();
        var deal = new Deal { Stage = DealStage.QualifiedToBuy, Status = DealStatus.Open };
        var record = new HubSpotRecord("1", new Dictionary<string, string?>
        {
            ["dealname"] = "Imported Deal",
            ["amount"] = "5000.50",
            ["dealstage"] = "closedwon",
            ["deal_currency_code"] = "usd"
        }, null, null);

        mapper.ApplyHubSpotProperties(deal, record);

        Assert.Equal("Imported Deal", deal.Name);
        Assert.Equal(5000.50m, deal.Amount);
        Assert.Equal(DealStage.ClosedWon, deal.Stage);
        Assert.Equal(DealStatus.Won, deal.Status);
        Assert.Equal("USD", deal.Currency);
    }

    [Fact]
    public void ApplyHubSpotProperties_IgnoresUnknownDealStage()
    {
        var mapper = CreateMapper();
        var deal = new Deal { Stage = DealStage.Proposal, Status = DealStatus.Open };
        var record = new HubSpotRecord("1", new Dictionary<string, string?>
        {
            ["dealstage"] = "some_custom_pipeline_stage_id"
        }, null, null);

        mapper.ApplyHubSpotProperties(deal, record);

        Assert.Equal(DealStage.Proposal, deal.Stage);
        Assert.Equal(DealStatus.Open, deal.Status);
    }
}
