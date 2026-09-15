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
    Pending,
    Processing,
    Completed,
    Failed,
    DeadLetter
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
