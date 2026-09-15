namespace CrmIntegration.Application.Sync;

/// <summary>
/// Result of a deterministic duplicate/match lookup. Deliberately has no fuzzy/AI matching —
/// only exact normalized-field equality counts as ExactMatch. Anything less certain is
/// Ambiguous, and Ambiguous must never be auto-selected/merged by a caller.
/// </summary>
public enum MatchType
{
    NoMatch,
    ExactMatch,
    Ambiguous
}

/// <summary>A match found among internal PostgreSQL entities (used during HubSpot -> Internal sync).</summary>
public record InternalMatchResult(MatchType MatchType, Guid? InternalId);

/// <summary>A match found among HubSpot CRM objects (used during Internal -> HubSpot sync).</summary>
public record HubSpotMatchResult(MatchType MatchType, string? HubSpotId);
