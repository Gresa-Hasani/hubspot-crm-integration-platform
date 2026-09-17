using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Reporting;

/// <summary>
/// A monetary total tagged with the currency it was summed in. Deal.Currency is per-deal (not a
/// single portal-wide setting), so every monetary aggregate in this reporting layer is grouped by
/// currency rather than summed across currencies — see docs/REPORTING.md "Currency policy".
/// </summary>
public record MoneyByCurrency(string Currency, decimal Amount);

public record SalesOverviewResponse(
    int TotalContacts,
    int TotalCompanies,
    int TotalDeals,
    int OpenDeals,
    int WonDeals,
    int LostDeals,
    IReadOnlyList<MoneyByCurrency> OpenPipelineValue,
    IReadOnlyList<MoneyByCurrency> WonRevenue,
    /// <summary>Average Deal.Amount across ALL deals (any stage/status), grouped by currency — distinct from RevenueReportResponse's average WON deal size.</summary>
    IReadOnlyList<MoneyByCurrency> AverageDealSize,
    /// <summary>WonDeals / (WonDeals + LostDeals). 0 when there are no closed deals yet (documented, not null/NaN).</summary>
    decimal WinRate,
    int OnboardingCount);

public record PipelineStageMetric(
    DealStage Stage,
    string Currency,
    int DealCount,
    decimal TotalAmount,
    decimal AverageAmount,
    /// <summary>Share of total OPEN pipeline value (same currency) this stage represents. Null for ClosedWon/ClosedLost rows — see docs/REPORTING.md "Pipeline definitions".</summary>
    decimal? PercentageOfOpenPipeline);

public record PipelineReportResponse(IReadOnlyList<PipelineStageMetric> Stages);

public enum RevenueGroupBy { Day, Month }

public record RevenueTimeBucket(DateOnly BucketStart, string Currency, decimal TotalAmount, int DealCount);

public record RevenueReportResponse(
    DateOnly? From,
    DateOnly? To,
    RevenueGroupBy GroupBy,
    IReadOnlyList<MoneyByCurrency> TotalWonRevenue,
    int WonDealCount,
    IReadOnlyList<MoneyByCurrency> AverageWonDealSize,
    IReadOnlyList<RevenueTimeBucket> Buckets,
    /// <summary>Won deals excluded from this report because they have no DealStageTransition recording when they reached ClosedWon (data predates Phase 6, or was seeded directly) — see docs/REPORTING.md "Closed Won revenue-date source".</summary>
    int ExcludedWonDealsWithoutTransitionHistory);

public record OutcomeReportResponse(
    DateOnly? From,
    DateOnly? To,
    int WonDealCount,
    int LostDealCount,
    int OpenDealCount,
    /// <summary>WonDealCount / (WonDealCount + LostDealCount). 0 when there are no closed deals.</summary>
    decimal WinRate,
    /// <summary>LostDealCount / (WonDealCount + LostDealCount). 0 when there are no closed deals.</summary>
    decimal LossRate,
    IReadOnlyList<MoneyByCurrency> TotalWonValue,
    IReadOnlyList<MoneyByCurrency> TotalLostValue,
    int ExcludedDealsWithoutTransitionHistory);

public record TransitionMetric(DealStage? FromStage, DealStage ToStage, int TransitionCount);

public record ConversionReportResponse(
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<TransitionMetric> ObservedTransitions,
    /// <summary>Always false: DealStage is not schema-enforced as a strict sequential funnel (deals may be created at any stage, and Phase 6 confirmed re-entry into ClosedWon is possible), so this report never synthesizes stage-to-stage conversion percentages. See docs/REPORTING.md "Conversion limitations".</summary>
    bool FunnelConversionRatesAvailable);

public record CycleTimeSample(Guid DealId, string DealName, DateTime CreatedAt, DateTime ClosedAt, double DurationDays);

public record StageDurationMetric(DealStage Stage, int SampleCount, double AverageDurationDays);

public record VelocityReportResponse(
    int WonDealsWithTransitionHistory,
    double? AverageDaysToClosedWon,
    double? MedianDaysToClosedWon,
    int LostDealsWithTransitionHistory,
    double? AverageDaysToClosedLost,
    IReadOnlyList<CycleTimeSample> RecentClosedWonCycleTimes,
    /// <summary>Average time spent in a stage, computed only from CLOSED intervals (a transition into the stage AND a later transition out of it both exist). A deal's current/most-recent stage is never included since its exit time is unknown. Empty until enough multi-hop transition history accumulates — see docs/REPORTING.md "Velocity limitations".</summary>
    IReadOnlyList<StageDurationMetric> AverageTimePerStage,
    int DealsExcludedWithoutTransitionHistory);

public record LifecycleStageMetric(LifecycleStage Stage, int ContactCount, decimal PercentageOfTotal);

public record LifecycleTransitionMetric(LifecycleStage? FromStage, LifecycleStage ToStage, int TransitionCount);

public record RecentLifecycleChange(Guid ContactId, string FirstName, string LastName, LifecycleStage? FromStage, LifecycleStage ToStage, DateTime OccurredAt);

public record LifecycleReportResponse(
    int TotalContacts,
    IReadOnlyList<LifecycleStageMetric> StageDistribution,
    IReadOnlyList<LifecycleTransitionMetric> ObservedTransitions,
    IReadOnlyList<RecentLifecycleChange> RecentChanges);

public record OnboardingStatusMetric(OnboardingStatus Status, int Count);

public record RecentOnboardingHandoff(
    Guid OnboardingRecordId,
    Guid DealId,
    string DealName,
    decimal DealAmount,
    string DealCurrency,
    string CompanyName,
    string? ContactName,
    OnboardingStatus Status,
    DateTime CreatedAt);

public record OnboardingReportResponse(
    int TotalOnboardingRecords,
    IReadOnlyList<OnboardingStatusMetric> ByStatus,
    IReadOnlyList<MoneyByCurrency> AssociatedDealValue,
    IReadOnlyList<RecentOnboardingHandoff> RecentHandoffs);

public enum SalesActivityType
{
    DealStageChanged,
    DealClosedWon,
    ContactLifecycleChanged,
    OnboardingCreated,
    AutomationCompleted
}

public record SalesActivityItem(
    SalesActivityType Type,
    DateTime OccurredAt,
    string Description,
    Guid EntityId,
    EntityType EntityType);

public record SalesActivityResponse(IReadOnlyList<SalesActivityItem> Items);
