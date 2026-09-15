using CrmIntegration.Application.Common;
using CrmIntegration.Application.Configuration;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CrmIntegration.Application.Integrations.HubSpot;

public class HubSpotStageMapper : IHubSpotStageMapper
{
    private readonly HubSpotOptions _options;

    public HubSpotStageMapper(IOptions<HubSpotOptions> options)
    {
        _options = options.Value;
    }

    public string ToHubSpotDealStage(DealStage stage) =>
        _options.DealStageMapping.TryGetValue(stage.ToString(), out var value)
            ? value
            : throw new DomainValidationException($"No HubSpot deal stage mapping configured for '{stage}'.");

    public DealStage? FromHubSpotDealStage(string hubSpotStageId)
    {
        foreach (var (internalName, externalId) in _options.DealStageMapping)
        {
            if (string.Equals(externalId, hubSpotStageId, StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<DealStage>(internalName, out var stage))
            {
                return stage;
            }
        }

        return null;
    }

    public string ToHubSpotLifecycleStage(LifecycleStage stage) =>
        _options.LifecycleStageMapping.TryGetValue(stage.ToString(), out var value)
            ? value
            : throw new DomainValidationException($"No HubSpot lifecycle stage mapping configured for '{stage}'.");

    public LifecycleStage? FromHubSpotLifecycleStage(string hubSpotValue)
    {
        foreach (var (internalName, externalValue) in _options.LifecycleStageMapping)
        {
            if (string.Equals(externalValue, hubSpotValue, StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<LifecycleStage>(internalName, out var stage))
            {
                return stage;
            }
        }

        return null;
    }
}
