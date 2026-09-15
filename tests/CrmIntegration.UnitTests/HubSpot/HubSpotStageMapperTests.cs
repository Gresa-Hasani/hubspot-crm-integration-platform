using CrmIntegration.Application.Common;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.HubSpot;

public class HubSpotStageMapperTests
{
    private static HubSpotStageMapper CreateMapper() =>
        new(Options.Create(new HubSpotOptions()));

    [Theory]
    [InlineData(DealStage.QualifiedToBuy, "qualifiedtobuy")]
    [InlineData(DealStage.ClosedWon, "closedwon")]
    [InlineData(DealStage.ClosedLost, "closedlost")]
    public void ToHubSpotDealStage_UsesConfiguredMapping(DealStage stage, string expected)
    {
        var mapper = CreateMapper();

        Assert.Equal(expected, mapper.ToHubSpotDealStage(stage));
    }

    [Fact]
    public void FromHubSpotDealStage_ReversesTheMapping()
    {
        var mapper = CreateMapper();

        Assert.Equal(DealStage.ClosedWon, mapper.FromHubSpotDealStage("closedwon"));
    }

    [Fact]
    public void FromHubSpotDealStage_ReturnsNull_ForUnknownStageId()
    {
        var mapper = CreateMapper();

        Assert.Null(mapper.FromHubSpotDealStage("some_custom_pipeline_stage_id_12345"));
    }

    [Fact]
    public void ToHubSpotDealStage_Throws_WhenMappingConfigurationIsIncomplete()
    {
        var options = new HubSpotOptions();
        options.DealStageMapping.Remove("Proposal");
        var mapper = new HubSpotStageMapper(Options.Create(options));

        Assert.Throws<DomainValidationException>(() => mapper.ToHubSpotDealStage(DealStage.Proposal));
    }

    [Theory]
    [InlineData(LifecycleStage.SalesQualifiedLead, "salesqualifiedlead")]
    [InlineData(LifecycleStage.Customer, "customer")]
    public void ToHubSpotLifecycleStage_UsesConfiguredMapping(LifecycleStage stage, string expected)
    {
        var mapper = CreateMapper();

        Assert.Equal(expected, mapper.ToHubSpotLifecycleStage(stage));
    }

    [Fact]
    public void FromHubSpotLifecycleStage_ReversesTheMapping()
    {
        var mapper = CreateMapper();

        Assert.Equal(LifecycleStage.SalesQualifiedLead, mapper.FromHubSpotLifecycleStage("salesqualifiedlead"));
    }
}
