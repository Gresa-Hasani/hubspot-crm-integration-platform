using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Integrations.HubSpot;

/// <summary>
/// Translates between internal Stage/LifecycleStage enums and HubSpot's portal-specific string
/// ids, using the explicit configuration in HubSpotOptions rather than assuming a naming
/// convention. Consumed by the Phase 4 sync engine.
/// </summary>
public interface IHubSpotStageMapper
{
    string ToHubSpotDealStage(DealStage stage);
    DealStage? FromHubSpotDealStage(string hubSpotStageId);

    string ToHubSpotLifecycleStage(LifecycleStage stage);
    LifecycleStage? FromHubSpotLifecycleStage(string hubSpotValue);
}
