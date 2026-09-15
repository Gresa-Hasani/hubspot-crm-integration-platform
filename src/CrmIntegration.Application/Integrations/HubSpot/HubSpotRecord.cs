namespace CrmIntegration.Application.Integrations.HubSpot;

/// <summary>
/// A HubSpot CRM object as returned by the API: opaque property bag plus HubSpot's own
/// identifiers/timestamps. Deliberately not the internal domain entity — the sync engine
/// (Phase 4) is responsible for translating between the two via explicit field mappings.
/// </summary>
public record HubSpotRecord(
    string Id,
    IReadOnlyDictionary<string, string?> Properties,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);
