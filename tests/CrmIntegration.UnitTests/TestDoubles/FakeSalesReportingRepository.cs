using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.UnitTests.TestDoubles;

/// <summary>
/// In-memory stand-in for ISalesReportingRepository: holds plain entity lists and answers each
/// method with the same LINQ shape the real (EF Core) repository uses, just against
/// LINQ-to-Objects instead of a translated SQL query. This lets reporting-service unit tests
/// exercise the exact formula/formatting logic deterministically without a database — the real
/// repository's actual SQL translation is verified separately by the PostgreSQL integration tests.
/// </summary>
public class FakeSalesReportingRepository : ISalesReportingRepository
{
    public List<Contact> Contacts { get; } = new();
    public List<Company> Companies { get; } = new();
    public List<Deal> Deals { get; } = new();
    public List<DealStageTransition> DealStageTransitions { get; } = new();
    public List<ContactLifecycleTransition> ContactLifecycleTransitions { get; } = new();
    public List<OnboardingRecord> OnboardingRecords { get; } = new();
    public List<AutomationExecution> AutomationExecutions { get; } = new();

    public Task<int> CountContactsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Contacts.Count);
    public Task<int> CountCompaniesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Companies.Count);
    public Task<int> CountOnboardingRecordsAsync(CancellationToken cancellationToken = default) => Task.FromResult(OnboardingRecords.Count);

    public Task<IReadOnlyList<DealStatusCountRow>> GetDealCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DealStatusCountRow>>(
            Deals.GroupBy(d => d.Status).Select(g => new DealStatusCountRow(g.Key, g.Count())).ToList());

    public Task<IReadOnlyList<MoneyByCurrency>> GetDealAmountSumByCurrencyAsync(DealStatus status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MoneyByCurrency>>(
            Deals.Where(d => d.Status == status).GroupBy(d => d.Currency).Select(g => new MoneyByCurrency(g.Key, g.Sum(d => d.Amount))).ToList());

    public Task<IReadOnlyList<CurrencyAverageRow>> GetAllDealsAverageAmountByCurrencyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CurrencyAverageRow>>(
            Deals.GroupBy(d => d.Currency).Select(g => new CurrencyAverageRow(g.Key, g.Average(d => d.Amount))).ToList());

    public Task<IReadOnlyList<DealStageAmountRow>> GetDealAmountsByStageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DealStageAmountRow>>(
            Deals.GroupBy(d => new { d.Stage, d.Currency })
                .Select(g => new DealStageAmountRow(g.Key.Stage, g.Key.Currency, g.Count(), g.Sum(d => d.Amount), g.Average(d => d.Amount)))
                .ToList());

    public Task<IReadOnlyList<ClosedDealRow>> GetClosedWonDealsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(GetClosedDeals(DealStatus.Won, DealStage.ClosedWon));

    public Task<IReadOnlyList<ClosedDealRow>> GetClosedLostDealsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(GetClosedDeals(DealStatus.Lost, DealStage.ClosedLost));

    private IReadOnlyList<ClosedDealRow> GetClosedDeals(DealStatus status, DealStage closedStage)
    {
        var latestByDeal = DealStageTransitions
            .Where(t => t.ToStage == closedStage)
            .GroupBy(t => t.DealId)
            .ToDictionary(g => g.Key, g => g.Max(t => t.OccurredAt));

        return Deals.Where(d => d.Status == status)
            .Select(d => new ClosedDealRow(d.Id, d.Name, d.Currency, d.Amount, d.CreatedAt, latestByDeal.TryGetValue(d.Id, out var t) ? t : (DateTime?)null))
            .ToList();
    }

    public Task<IReadOnlyList<DealTransitionCountRow>> GetDealStageTransitionCountsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var query = DealStageTransitions.AsEnumerable();
        if (fromUtc is DateTime f) query = query.Where(t => t.OccurredAt >= f);
        if (toUtc is DateTime t2) query = query.Where(t => t.OccurredAt <= t2);

        return Task.FromResult<IReadOnlyList<DealTransitionCountRow>>(
            query.GroupBy(t => new { t.FromStage, t.ToStage })
                .Select(g => new DealTransitionCountRow(g.Key.FromStage, g.Key.ToStage, g.Count()))
                .ToList());
    }

    public Task<IReadOnlyList<DealTransitionSequenceRow>> GetAllDealStageTransitionsOrderedAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DealTransitionSequenceRow>>(
            DealStageTransitions.OrderBy(t => t.DealId).ThenBy(t => t.OccurredAt)
                .Select(t => new DealTransitionSequenceRow(t.DealId, t.FromStage, t.ToStage, t.OccurredAt))
                .ToList());

    public Task<IReadOnlyList<LifecycleStageCountRow>> GetContactCountsByLifecycleStageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LifecycleStageCountRow>>(
            Contacts.GroupBy(c => c.LifecycleStage).Select(g => new LifecycleStageCountRow(g.Key, g.Count())).ToList());

    public Task<IReadOnlyList<LifecycleTransitionCountRow>> GetContactLifecycleTransitionCountsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LifecycleTransitionCountRow>>(
            ContactLifecycleTransitions.GroupBy(t => new { t.FromStage, t.ToStage })
                .Select(g => new LifecycleTransitionCountRow(g.Key.FromStage, g.Key.ToStage, g.Count()))
                .ToList());

    public Task<IReadOnlyList<RecentLifecycleChangeRow>> GetRecentLifecycleChangesAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecentLifecycleChangeRow>>(
            (from t in ContactLifecycleTransitions
             join c in Contacts on t.ContactId equals c.Id
             orderby t.OccurredAt descending
             select new RecentLifecycleChangeRow(c.Id, c.FirstName, c.LastName, t.FromStage, t.ToStage, t.OccurredAt))
            .Take(limit).ToList());

    public Task<IReadOnlyList<OnboardingStatusCountRow>> GetOnboardingCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OnboardingStatusCountRow>>(
            OnboardingRecords.GroupBy(o => o.Status).Select(g => new OnboardingStatusCountRow(g.Key, g.Count())).ToList());

    public Task<IReadOnlyList<MoneyByCurrency>> GetOnboardingAssociatedDealValueByCurrencyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MoneyByCurrency>>(
            (from o in OnboardingRecords
             join d in Deals on o.DealId equals d.Id
             group d by d.Currency into g
             select new MoneyByCurrency(g.Key, g.Sum(d => d.Amount)))
            .ToList());

    public Task<IReadOnlyList<RecentOnboardingHandoffRow>> GetRecentOnboardingHandoffsAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RecentOnboardingHandoffRow>>(
            (from o in OnboardingRecords
             join d in Deals on o.DealId equals d.Id
             join co in Companies on o.CompanyId equals co.Id
             orderby o.CreatedAt descending
             select new RecentOnboardingHandoffRow(
                 o.Id, d.Id, d.Name, d.Amount, d.Currency, co.Name,
                 Contacts.FirstOrDefault(c => c.Id == o.ContactId) != null ? Contacts.First(c => c.Id == o.ContactId).FirstName : null,
                 Contacts.FirstOrDefault(c => c.Id == o.ContactId) != null ? Contacts.First(c => c.Id == o.ContactId).LastName : null,
                 o.Status, o.CreatedAt))
            .Take(limit).ToList());

    public Task<IReadOnlyList<DealStageTransitionActivityRow>> GetRecentDealStageTransitionsAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DealStageTransitionActivityRow>>(
            (from t in DealStageTransitions
             join d in Deals on t.DealId equals d.Id
             orderby t.OccurredAt descending
             select new DealStageTransitionActivityRow(d.Id, d.Name, t.FromStage, t.ToStage, t.OccurredAt))
            .Take(limit).ToList());

    public Task<IReadOnlyList<ContactLifecycleTransitionActivityRow>> GetRecentContactLifecycleTransitionsAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ContactLifecycleTransitionActivityRow>>(
            (from t in ContactLifecycleTransitions
             join c in Contacts on t.ContactId equals c.Id
             orderby t.OccurredAt descending
             select new ContactLifecycleTransitionActivityRow(c.Id, c.FirstName, c.LastName, t.FromStage, t.ToStage, t.OccurredAt))
            .Take(limit).ToList());

    public Task<IReadOnlyList<AutomationExecutionActivityRow>> GetRecentTerminalAutomationExecutionsAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AutomationExecutionActivityRow>>(
            AutomationExecutions.Where(a => a.Status is AutomationStatus.Succeeded or AutomationStatus.Skipped or AutomationStatus.Failed)
                .OrderByDescending(a => a.CompletedAt ?? a.StartedAt)
                .Select(a => new AutomationExecutionActivityRow(a.Id, a.AutomationType, a.EntityType, a.EntityId, a.Status, a.ResultSummary, a.CompletedAt ?? a.StartedAt))
                .Take(limit).ToList());

    public Task<IReadOnlyList<OnboardingActivityRow>> GetRecentOnboardingCreationsAsync(int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OnboardingActivityRow>>(
            (from o in OnboardingRecords
             join d in Deals on o.DealId equals d.Id
             orderby o.CreatedAt descending
             select new OnboardingActivityRow(o.Id, d.Id, d.Name, o.CreatedAt))
            .Take(limit).ToList());
}
