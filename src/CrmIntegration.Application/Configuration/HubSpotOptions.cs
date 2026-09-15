namespace CrmIntegration.Application.Configuration;

public class HubSpotOptions
{
    public const string SectionName = "HubSpot";

    public string BaseUrl { get; set; } = "https://api.hubapi.com";
    public string AccessToken { get; set; } = string.Empty;
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? WebhookSigningSecret { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to send deal_currency_code on create/update. Off by default: most HubSpot portals
    /// don't have multi-currency enabled, and HubSpot rejects deal_currency_code with a 400
    /// (INVALID_OPTION) if the value isn't one of the portal's configured currencies — discovered
    /// against a real test portal during Phase 4 live verification. Enable only if your portal has
    /// multi-currency turned on and the currencies used actually match its configuration.
    /// </summary>
    public bool SyncDealCurrencyCode { get; set; }

    /// <summary>
    /// Internal DealStage enum name -> HubSpot pipeline stage id. HubSpot stage ids are portal-
    /// specific (custom pipelines use generated numeric ids), so this must never be assumed —
    /// it's configured per environment. Defaults below match HubSpot's out-of-the-box "Sales"
    /// pipeline; override in appsettings/environment for a real portal's actual stage ids.
    /// </summary>
    public Dictionary<string, string> DealStageMapping { get; set; } = new()
    {
        ["QualifiedToBuy"] = "qualifiedtobuy",
        ["Proposal"] = "presentationscheduled",
        ["Negotiation"] = "contractsent",
        ["ClosedWon"] = "closedwon",
        ["ClosedLost"] = "closedlost"
    };

    /// <summary>Internal LifecycleStage enum name -> HubSpot lifecyclestage property value.</summary>
    public Dictionary<string, string> LifecycleStageMapping { get; set; } = new()
    {
        ["Subscriber"] = "subscriber",
        ["Lead"] = "lead",
        ["MarketingQualifiedLead"] = "marketingqualifiedlead",
        ["SalesQualifiedLead"] = "salesqualifiedlead",
        ["Opportunity"] = "opportunity",
        ["Customer"] = "customer",
        ["Evangelist"] = "evangelist",
        ["Other"] = "other"
    };
}
