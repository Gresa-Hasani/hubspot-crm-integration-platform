namespace CrmIntegration.Application.Integrations.HubSpot;

public enum HubSpotObjectType
{
    Contact,
    Company,
    Deal
}

public static class HubSpotObjectTypeExtensions
{
    /// <summary>The plural path segment HubSpot's CRM v3/v4 APIs use, e.g. /crm/v3/objects/{plural}.</summary>
    public static string ToApiPath(this HubSpotObjectType type) => type switch
    {
        HubSpotObjectType.Contact => "contacts",
        HubSpotObjectType.Company => "companies",
        HubSpotObjectType.Deal => "deals",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };
}
