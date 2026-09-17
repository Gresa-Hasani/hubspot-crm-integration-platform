using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Reporting;

/// <summary>
/// Read-only, aggregation-oriented queries backing the sales reporting endpoints. Every method
/// executes its aggregation in PostgreSQL (GroupBy/Sum/Count/Average projections, AsNoTracking) —
/// none of them load full entity graphs or all rows into memory before aggregating.
/// </summary>
public interface ISalesReportingRepository
{
    Task<int> CountContactsAsync(CancellationToken cancellationToken = default);
    Task<int> CountCompaniesAsync(CancellationToken cancellationToken = default);
    Task<int> CountOnboardingRecordsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DealStatusCountRow>> GetDealCountsByStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MoneyByCurrency>> GetDealAmountSumByCurrencyAsync(DealStatus status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CurrencyAverageRow>> GetAllDealsAverageAmountByCurrencyAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DealStageAmountRow>> GetDealAmountsByStageAsync(CancellationToken cancellationToken = default);

    /// <summary>All Deals with Status=Won, each with the latest DealStageTransition.OccurredAt into ClosedWon (null if no such transition exists).</summary>
    Task<IReadOnlyList<ClosedDealRow>> GetClosedWonDealsAsync(CancellationToken cancellationToken = default);

    /// <summary>All Deals with Status=Lost, each with the latest DealStageTransition.OccurredAt into ClosedLost (null if no such transition exists).</summary>
    Task<IReadOnlyList<ClosedDealRow>> GetClosedLostDealsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DealTransitionCountRow>> GetDealStageTransitionCountsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default);

    /// <summary>Every DealStageTransition, ordered by (DealId, OccurredAt), for stage-duration calculation.</summary>
    Task<IReadOnlyList<DealTransitionSequenceRow>> GetAllDealStageTransitionsOrderedAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LifecycleStageCountRow>> GetContactCountsByLifecycleStageAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LifecycleTransitionCountRow>> GetContactLifecycleTransitionCountsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecentLifecycleChangeRow>> GetRecentLifecycleChangesAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OnboardingStatusCountRow>> GetOnboardingCountsByStatusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MoneyByCurrency>> GetOnboardingAssociatedDealValueByCurrencyAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecentOnboardingHandoffRow>> GetRecentOnboardingHandoffsAsync(int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DealStageTransitionActivityRow>> GetRecentDealStageTransitionsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactLifecycleTransitionActivityRow>> GetRecentContactLifecycleTransitionsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationExecutionActivityRow>> GetRecentTerminalAutomationExecutionsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OnboardingActivityRow>> GetRecentOnboardingCreationsAsync(int limit, CancellationToken cancellationToken = default);
}
