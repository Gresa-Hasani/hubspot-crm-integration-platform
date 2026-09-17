using CrmIntegration.Application.Common;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Reporting;

public class SalesReportingService : ISalesReportingService
{
    private const int DefaultActivityLimit = 50;
    private const int MaxActivityLimit = 200;
    private const int RecentSampleLimit = 10;

    private readonly ISalesReportingRepository _repository;

    public SalesReportingService(ISalesReportingRepository repository)
    {
        _repository = repository;
    }

    public async Task<SalesOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var totalContacts = await _repository.CountContactsAsync(cancellationToken);
        var totalCompanies = await _repository.CountCompaniesAsync(cancellationToken);
        var statusCounts = await _repository.GetDealCountsByStatusAsync(cancellationToken);
        var openPipeline = await _repository.GetDealAmountSumByCurrencyAsync(DealStatus.Open, cancellationToken);
        var wonRevenue = await _repository.GetDealAmountSumByCurrencyAsync(DealStatus.Won, cancellationToken);
        var averageDealSize = await _repository.GetAllDealsAverageAmountByCurrencyAsync(cancellationToken);
        var onboardingCount = await _repository.CountOnboardingRecordsAsync(cancellationToken);

        var openDeals = statusCounts.FirstOrDefault(s => s.Status == DealStatus.Open)?.Count ?? 0;
        var wonDeals = statusCounts.FirstOrDefault(s => s.Status == DealStatus.Won)?.Count ?? 0;
        var lostDeals = statusCounts.FirstOrDefault(s => s.Status == DealStatus.Lost)?.Count ?? 0;
        var totalDeals = openDeals + wonDeals + lostDeals;

        return new SalesOverviewResponse(
            totalContacts,
            totalCompanies,
            totalDeals,
            openDeals,
            wonDeals,
            lostDeals,
            openPipeline,
            wonRevenue,
            averageDealSize.Select(a => new MoneyByCurrency(a.Currency, Math.Round(a.Average, 2))).ToList(),
            SafeRate(wonDeals, wonDeals + lostDeals),
            onboardingCount);
    }

    public async Task<PipelineReportResponse> GetPipelineAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _repository.GetDealAmountsByStageAsync(cancellationToken);

        // "Open pipeline" is the sum of amounts in the three non-terminal stages, per currency —
        // ClosedWon/ClosedLost are never counted as pipeline (see docs/REPORTING.md).
        var openPipelineTotalsByCurrency = rows
            .Where(r => IsOpenStage(r.Stage))
            .GroupBy(r => r.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));

        var stages = rows.Select(r => new PipelineStageMetric(
            r.Stage,
            r.Currency,
            r.Count,
            r.TotalAmount,
            Math.Round(r.AverageAmount, 2),
            IsOpenStage(r.Stage) && openPipelineTotalsByCurrency.TryGetValue(r.Currency, out var total) && total != 0m
                ? Math.Round(r.TotalAmount / total * 100m, 2)
                : (decimal?)null
        )).ToList();

        return new PipelineReportResponse(stages);
    }

    public async Task<RevenueReportResponse> GetRevenueAsync(string? from, string? to, string? groupBy, CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, fromUtc, toUtc) = ReportingDateRange.Parse(from, to);
        var bucketing = ParseGroupBy(groupBy);

        var wonDeals = await _repository.GetClosedWonDealsAsync(cancellationToken);

        var withTransition = wonDeals.Where(d => d.ClosedAtUtc is not null).ToList();
        var excludedCount = wonDeals.Count - withTransition.Count;

        var inRange = withTransition
            .Where(d => (fromUtc is null || d.ClosedAtUtc >= fromUtc) && (toUtc is null || d.ClosedAtUtc <= toUtc))
            .ToList();

        var totalWonRevenue = inRange
            .GroupBy(d => d.Currency)
            .Select(g => new MoneyByCurrency(g.Key, g.Sum(d => d.Amount)))
            .OrderBy(m => m.Currency)
            .ToList();

        var averageWonDealSize = inRange
            .GroupBy(d => d.Currency)
            .Select(g => new MoneyByCurrency(g.Key, Math.Round(g.Average(d => d.Amount), 2)))
            .OrderBy(m => m.Currency)
            .ToList();

        var buckets = inRange
            .GroupBy(d => (BucketStart: BucketStartOf(d.ClosedAtUtc!.Value, bucketing), d.Currency))
            .Select(g => new RevenueTimeBucket(g.Key.BucketStart, g.Key.Currency, g.Sum(d => d.Amount), g.Count()))
            .OrderBy(b => b.BucketStart).ThenBy(b => b.Currency)
            .ToList();

        return new RevenueReportResponse(fromDate, toDate, bucketing, totalWonRevenue, inRange.Count, averageWonDealSize, buckets, excludedCount);
    }

    public async Task<OutcomeReportResponse> GetOutcomesAsync(string? from, string? to, CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, fromUtc, toUtc) = ReportingDateRange.Parse(from, to);
        var hasRange = fromUtc is not null || toUtc is not null;

        var wonDeals = await _repository.GetClosedWonDealsAsync(cancellationToken);
        var lostDeals = await _repository.GetClosedLostDealsAsync(cancellationToken);
        var statusCounts = await _repository.GetDealCountsByStatusAsync(cancellationToken);
        var openDealCount = statusCounts.FirstOrDefault(s => s.Status == DealStatus.Open)?.Count ?? 0;

        int excludedCount;
        List<ClosedDealRow> wonInScope;
        List<ClosedDealRow> lostInScope;

        if (hasRange)
        {
            // A date filter can only be honored using the ClosedWon/ClosedLost transition
            // timestamp — deals without that history are excluded and counted, exactly like the
            // Revenue report.
            var wonWithTransition = wonDeals.Where(d => d.ClosedAtUtc is not null).ToList();
            var lostWithTransition = lostDeals.Where(d => d.ClosedAtUtc is not null).ToList();
            excludedCount = (wonDeals.Count - wonWithTransition.Count) + (lostDeals.Count - lostWithTransition.Count);

            wonInScope = wonWithTransition.Where(d => (fromUtc is null || d.ClosedAtUtc >= fromUtc) && (toUtc is null || d.ClosedAtUtc <= toUtc)).ToList();
            lostInScope = lostWithTransition.Where(d => (fromUtc is null || d.ClosedAtUtc >= fromUtc) && (toUtc is null || d.ClosedAtUtc <= toUtc)).ToList();
        }
        else
        {
            // No date filter: every Won/Lost deal counts, regardless of transition history.
            wonInScope = wonDeals.ToList();
            lostInScope = lostDeals.ToList();
            excludedCount = 0;
        }

        var totalWonValue = wonInScope.GroupBy(d => d.Currency).Select(g => new MoneyByCurrency(g.Key, g.Sum(d => d.Amount))).OrderBy(m => m.Currency).ToList();
        var totalLostValue = lostInScope.GroupBy(d => d.Currency).Select(g => new MoneyByCurrency(g.Key, g.Sum(d => d.Amount))).OrderBy(m => m.Currency).ToList();

        var closedCount = wonInScope.Count + lostInScope.Count;

        return new OutcomeReportResponse(
            fromDate, toDate,
            wonInScope.Count, lostInScope.Count, openDealCount,
            SafeRate(wonInScope.Count, closedCount),
            SafeRate(lostInScope.Count, closedCount),
            totalWonValue, totalLostValue,
            excludedCount);
    }

    public async Task<ConversionReportResponse> GetConversionAsync(string? from, string? to, CancellationToken cancellationToken = default)
    {
        var (fromDate, toDate, fromUtc, toUtc) = ReportingDateRange.Parse(from, to);
        var rows = await _repository.GetDealStageTransitionCountsAsync(fromUtc, toUtc, cancellationToken);

        var transitions = rows
            .Select(r => new TransitionMetric(r.FromStage, r.ToStage, r.Count))
            .OrderByDescending(t => t.TransitionCount)
            .ToList();

        // DealStage is not schema-enforced as a strict sequential funnel: deals may be created
        // directly at any stage, and re-entry into ClosedWon after leaving it is possible (see
        // Phase 6's live verification) — so this report only ever exposes observed transition
        // counts, never a synthesized funnel conversion rate. See docs/REPORTING.md "Conversion
        // limitations".
        return new ConversionReportResponse(fromDate, toDate, transitions, FunnelConversionRatesAvailable: false);
    }

    public async Task<VelocityReportResponse> GetVelocityAsync(CancellationToken cancellationToken = default)
    {
        var wonDeals = await _repository.GetClosedWonDealsAsync(cancellationToken);
        var lostDeals = await _repository.GetClosedLostDealsAsync(cancellationToken);

        var wonWithTransition = wonDeals.Where(d => d.ClosedAtUtc is not null).ToList();
        var lostWithTransition = lostDeals.Where(d => d.ClosedAtUtc is not null).ToList();
        var excluded = (wonDeals.Count - wonWithTransition.Count) + (lostDeals.Count - lostWithTransition.Count);

        var wonDurations = wonWithTransition.Select(d => (d.ClosedAtUtc!.Value - d.CreatedAtUtc).TotalDays).ToList();
        var lostDurations = lostWithTransition.Select(d => (d.ClosedAtUtc!.Value - d.CreatedAtUtc).TotalDays).ToList();

        var recentClosedWon = wonWithTransition
            .OrderByDescending(d => d.ClosedAtUtc)
            .Take(RecentSampleLimit)
            .Select(d => new CycleTimeSample(d.DealId, d.DealName, d.CreatedAtUtc, d.ClosedAtUtc!.Value, Math.Round((d.ClosedAtUtc.Value - d.CreatedAtUtc).TotalDays, 2)))
            .ToList();

        var transitionSequence = await _repository.GetAllDealStageTransitionsOrderedAsync(cancellationToken);
        var stageDurations = ComputeAverageTimePerStage(transitionSequence);

        return new VelocityReportResponse(
            wonWithTransition.Count,
            wonDurations.Count > 0 ? Math.Round(wonDurations.Average(), 2) : null,
            wonDurations.Count > 0 ? Math.Round(Median(wonDurations), 2) : null,
            lostWithTransition.Count,
            lostDurations.Count > 0 ? Math.Round(lostDurations.Average(), 2) : null,
            recentClosedWon,
            stageDurations,
            excluded);
    }

    public async Task<LifecycleReportResponse> GetLifecycleAsync(CancellationToken cancellationToken = default)
    {
        var stageCounts = await _repository.GetContactCountsByLifecycleStageAsync(cancellationToken);
        var transitionCounts = await _repository.GetContactLifecycleTransitionCountsAsync(cancellationToken);
        var recentChanges = await _repository.GetRecentLifecycleChangesAsync(RecentSampleLimit, cancellationToken);

        var total = stageCounts.Sum(s => s.Count);

        var distribution = stageCounts
            .Select(s => new LifecycleStageMetric(s.Stage, s.Count, total > 0 ? Math.Round((decimal)s.Count / total * 100m, 2) : 0m))
            .OrderByDescending(s => s.ContactCount)
            .ToList();

        var transitions = transitionCounts
            .Select(t => new LifecycleTransitionMetric(t.FromStage, t.ToStage, t.Count))
            .OrderByDescending(t => t.TransitionCount)
            .ToList();

        var recent = recentChanges
            .Select(r => new RecentLifecycleChange(r.ContactId, r.FirstName, r.LastName, r.FromStage, r.ToStage, r.OccurredAtUtc))
            .ToList();

        return new LifecycleReportResponse(total, distribution, transitions, recent);
    }

    public async Task<OnboardingReportResponse> GetOnboardingAsync(CancellationToken cancellationToken = default)
    {
        var byStatusRows = await _repository.GetOnboardingCountsByStatusAsync(cancellationToken);
        var dealValue = await _repository.GetOnboardingAssociatedDealValueByCurrencyAsync(cancellationToken);
        var recentRows = await _repository.GetRecentOnboardingHandoffsAsync(RecentSampleLimit, cancellationToken);

        var total = byStatusRows.Sum(s => s.Count);
        var byStatus = byStatusRows.Select(s => new OnboardingStatusMetric(s.Status, s.Count)).ToList();

        var recent = recentRows.Select(r => new RecentOnboardingHandoff(
            r.OnboardingRecordId, r.DealId, r.DealName, r.DealAmount, r.DealCurrency,
            r.CompanyName,
            r.ContactFirstName is not null ? $"{r.ContactFirstName} {r.ContactLastName}" : null,
            r.Status, r.CreatedAtUtc)).ToList();

        return new OnboardingReportResponse(total, byStatus, dealValue, recent);
    }

    public async Task<SalesActivityResponse> GetActivityAsync(int limit, CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit <= 0 ? DefaultActivityLimit : limit, 1, MaxActivityLimit);

        var stageTransitions = await _repository.GetRecentDealStageTransitionsAsync(boundedLimit, cancellationToken);
        var lifecycleTransitions = await _repository.GetRecentContactLifecycleTransitionsAsync(boundedLimit, cancellationToken);
        var automations = await _repository.GetRecentTerminalAutomationExecutionsAsync(boundedLimit, cancellationToken);
        var onboardings = await _repository.GetRecentOnboardingCreationsAsync(boundedLimit, cancellationToken);

        var items = new List<SalesActivityItem>();

        items.AddRange(stageTransitions.Select(t => new SalesActivityItem(
            t.ToStage == DealStage.ClosedWon ? SalesActivityType.DealClosedWon : SalesActivityType.DealStageChanged,
            t.OccurredAtUtc,
            t.ToStage == DealStage.ClosedWon
                ? $"Deal '{t.DealName}' became Closed Won"
                : $"Deal '{t.DealName}' stage changed {(t.FromStage is null ? "(new)" : t.FromStage.ToString())} → {t.ToStage}",
            t.DealId, EntityType.Deal)));

        items.AddRange(lifecycleTransitions.Select(t => new SalesActivityItem(
            SalesActivityType.ContactLifecycleChanged,
            t.OccurredAtUtc,
            $"Contact '{t.FirstName} {t.LastName}' lifecycle changed {(t.FromStage is null ? "(new)" : t.FromStage.ToString())} → {t.ToStage}",
            t.ContactId, EntityType.Contact)));

        items.AddRange(automations.Select(a => new SalesActivityItem(
            SalesActivityType.AutomationCompleted,
            a.OccurredAtUtc,
            $"Automation {a.AutomationType} {a.Status} for {a.EntityType} {a.EntityId}" + (a.ResultSummary is not null ? $" ({a.ResultSummary})" : ""),
            a.EntityId, a.EntityType)));

        items.AddRange(onboardings.Select(o => new SalesActivityItem(
            SalesActivityType.OnboardingCreated,
            o.OccurredAtUtc,
            $"Onboarding created for Deal '{o.DealName}'",
            o.DealId, EntityType.Deal)));

        var top = items.OrderByDescending(i => i.OccurredAt).Take(boundedLimit).ToList();
        return new SalesActivityResponse(top);
    }

    private static bool IsOpenStage(DealStage stage) => stage is not (DealStage.ClosedWon or DealStage.ClosedLost);

    private static decimal SafeRate(int numerator, int denominator) =>
        denominator == 0 ? 0m : Math.Round((decimal)numerator / denominator * 100m, 2);

    private static RevenueGroupBy ParseGroupBy(string? groupBy) => groupBy?.Trim().ToLowerInvariant() switch
    {
        null or "" or "month" => RevenueGroupBy.Month,
        "day" => RevenueGroupBy.Day,
        _ => throw new DomainValidationException($"'groupBy' must be 'day' or 'month'; got '{groupBy}'.")
    };

    private static DateOnly BucketStartOf(DateTime utc, RevenueGroupBy groupBy) => groupBy switch
    {
        RevenueGroupBy.Day => DateOnly.FromDateTime(utc),
        RevenueGroupBy.Month => new DateOnly(utc.Year, utc.Month, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy))
    };

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    /// <summary>
    /// For each Deal's ordered transition history, the duration spent in stage T is the time
    /// between the transition that set ToStage=T and the NEXT transition for that same Deal
    /// (whatever it moves to). This only ever measures CLOSED intervals — a deal's current
    /// (most recent) stage has no "next transition" yet, so it is never included, and is not
    /// estimated using "now" to avoid mixing open-ended and closed durations in one average.
    /// </summary>
    private static List<StageDurationMetric> ComputeAverageTimePerStage(IReadOnlyList<DealTransitionSequenceRow> orderedTransitions)
    {
        var durationsByStage = new Dictionary<DealStage, List<double>>();

        foreach (var dealGroup in orderedTransitions.GroupBy(t => t.DealId))
        {
            var ordered = dealGroup.OrderBy(t => t.OccurredAtUtc).ToList();
            for (var i = 0; i < ordered.Count - 1; i++)
            {
                var stage = ordered[i].ToStage;
                var duration = (ordered[i + 1].OccurredAtUtc - ordered[i].OccurredAtUtc).TotalDays;
                if (!durationsByStage.TryGetValue(stage, out var list))
                {
                    list = new List<double>();
                    durationsByStage[stage] = list;
                }
                list.Add(duration);
            }
        }

        return durationsByStage
            .Select(kvp => new StageDurationMetric(kvp.Key, kvp.Value.Count, Math.Round(kvp.Value.Average(), 2)))
            .OrderBy(m => m.Stage)
            .ToList();
    }
}
