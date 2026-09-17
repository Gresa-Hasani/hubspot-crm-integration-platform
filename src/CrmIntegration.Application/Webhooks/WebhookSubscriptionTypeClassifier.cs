using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Webhooks;

/// <summary>
/// Deterministic policy for which HubSpot subscriptionType values this platform processes.
///
/// Policy (see docs/WEBHOOKS.md "Supported events"): every syntactically valid event is
/// persisted (durability/auditability first), but only Contact/Company/Deal creation and
/// propertyChange events are actually processed — everything else (deletions, associations,
/// other object types, privacy events) is persisted with Status=Ignored and never queued for
/// processing. This is a deliberate simplification: Phase 5 refreshes CRM objects into
/// PostgreSQL via the Phase 4 sync services, which have no delete/association-only path yet.
/// </summary>
public static class WebhookSubscriptionTypeClassifier
{
    private static readonly HashSet<string> SupportedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "creation",
        "propertyChange"
    };

    /// <summary>Returns the internal EntityType if the subscriptionType's object type is one this platform tracks, regardless of whether the action is processed.</summary>
    public static EntityType? TryGetEntityType(string subscriptionType)
    {
        var objectPart = subscriptionType.Split('.', 2)[0];
        return objectPart.ToLowerInvariant() switch
        {
            "contact" => EntityType.Contact,
            "company" => EntityType.Company,
            "deal" => EntityType.Deal,
            _ => null
        };
    }

    /// <summary>Whether this subscriptionType should actually be processed (vs. persisted-and-ignored).</summary>
    public static bool IsSupported(string subscriptionType)
    {
        if (TryGetEntityType(subscriptionType) is null)
        {
            return false;
        }

        var parts = subscriptionType.Split('.', 2);
        return parts.Length == 2 && SupportedActions.Contains(parts[1]);
    }
}
