namespace CrmIntegration.Domain.Enums;

public enum LifecycleStage
{
    Subscriber,
    Lead,
    MarketingQualifiedLead,
    SalesQualifiedLead,
    Opportunity,
    Customer,
    Evangelist,
    Other
}

public enum DealStage
{
    QualifiedToBuy,
    Proposal,
    Negotiation,
    ClosedWon,
    ClosedLost
}

public enum DealStatus
{
    Open,
    Won,
    Lost
}

public enum OnboardingStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

public enum ExternalSystem
{
    HubSpot
}

public enum EntityType
{
    Contact,
    Company,
    Deal
}

public enum IntegrationEventStatus
{
    Received,
    Processing,
    Processed,
    Failed,
    DeadLettered,
    Ignored
}

public enum SyncDirection
{
    HubSpotToInternal,
    InternalToHubSpot
}

public enum SyncStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    DeadLettered
}

/// <summary>Where a Deal stage / Contact lifecycle-stage change originated, for DealStageTransition/ContactLifecycleTransition.</summary>
public enum TransitionSource
{
    HubSpotWebhook,
    HubSpotSync,
    InternalUpdate,
    ManualSync
}

public enum AutomationType
{
    DealClosedWonOnboarding,
    ContactLifecycleTransition
}

public enum AutomationStatus
{
    Pending,
    Running,
    Succeeded,
    Skipped,
    Failed
}
