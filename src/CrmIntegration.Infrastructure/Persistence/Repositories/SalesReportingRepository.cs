using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class SalesReportingRepository : ISalesReportingRepository
{
    private readonly AppDbContext _context;

    public SalesReportingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> CountContactsAsync(CancellationToken cancellationToken = default) =>
        _context.Contacts.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountCompaniesAsync(CancellationToken cancellationToken = default) =>
        _context.Companies.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountOnboardingRecordsAsync(CancellationToken cancellationToken = default) =>
        _context.OnboardingRecords.AsNoTracking().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<DealStatusCountRow>> GetDealCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        await _context.Deals.AsNoTracking()
            .GroupBy(d => d.Status)
            .Select(g => new DealStatusCountRow(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MoneyByCurrency>> GetDealAmountSumByCurrencyAsync(DealStatus status, CancellationToken cancellationToken = default) =>
        await _context.Deals.AsNoTracking()
            .Where(d => d.Status == status)
            .GroupBy(d => d.Currency)
            .Select(g => new MoneyByCurrency(g.Key, g.Sum(d => d.Amount)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CurrencyAverageRow>> GetAllDealsAverageAmountByCurrencyAsync(CancellationToken cancellationToken = default) =>
        await _context.Deals.AsNoTracking()
            .GroupBy(d => d.Currency)
            .Select(g => new CurrencyAverageRow(g.Key, g.Average(d => d.Amount)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DealStageAmountRow>> GetDealAmountsByStageAsync(CancellationToken cancellationToken = default) =>
        await _context.Deals.AsNoTracking()
            .GroupBy(d => new { d.Stage, d.Currency })
            .Select(g => new DealStageAmountRow(g.Key.Stage, g.Key.Currency, g.Count(), g.Sum(d => d.Amount), g.Average(d => d.Amount)))
            .ToListAsync(cancellationToken);

    public Task<IReadOnlyList<ClosedDealRow>> GetClosedWonDealsAsync(CancellationToken cancellationToken = default) =>
        GetClosedDealsAsync(DealStatus.Won, DealStage.ClosedWon, cancellationToken);

    public Task<IReadOnlyList<ClosedDealRow>> GetClosedLostDealsAsync(CancellationToken cancellationToken = default) =>
        GetClosedDealsAsync(DealStatus.Lost, DealStage.ClosedLost, cancellationToken);

    private async Task<IReadOnlyList<ClosedDealRow>> GetClosedDealsAsync(DealStatus status, DealStage closedStage, CancellationToken cancellationToken)
    {
        var latestTransitionByDeal = _context.DealStageTransitions.AsNoTracking()
            .Where(t => t.ToStage == closedStage)
            .GroupBy(t => t.DealId)
            .Select(g => new { DealId = g.Key, ClosedAt = g.Max(t => t.OccurredAt) });

        var query =
            from d in _context.Deals.AsNoTracking()
            where d.Status == status
            join t in latestTransitionByDeal on d.Id equals t.DealId into joined
            from t in joined.DefaultIfEmpty()
            select new ClosedDealRow(d.Id, d.Name, d.Currency, d.Amount, d.CreatedAt, t == null ? (DateTime?)null : t.ClosedAt);

        var rows = await query.ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<DealTransitionCountRow>> GetDealStageTransitionCountsAsync(DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken = default)
    {
        var query = _context.DealStageTransitions.AsNoTracking().AsQueryable();
        if (fromUtc is DateTime f)
        {
            query = query.Where(t => t.OccurredAt >= f);
        }
        if (toUtc is DateTime t2)
        {
            query = query.Where(t => t.OccurredAt <= t2);
        }

        return await query
            .GroupBy(t => new { t.FromStage, t.ToStage })
            .Select(g => new DealTransitionCountRow(g.Key.FromStage, g.Key.ToStage, g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DealTransitionSequenceRow>> GetAllDealStageTransitionsOrderedAsync(CancellationToken cancellationToken = default) =>
        await _context.DealStageTransitions.AsNoTracking()
            .OrderBy(t => t.DealId).ThenBy(t => t.OccurredAt)
            .Select(t => new DealTransitionSequenceRow(t.DealId, t.FromStage, t.ToStage, t.OccurredAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LifecycleStageCountRow>> GetContactCountsByLifecycleStageAsync(CancellationToken cancellationToken = default) =>
        await _context.Contacts.AsNoTracking()
            .GroupBy(c => c.LifecycleStage)
            .Select(g => new LifecycleStageCountRow(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LifecycleTransitionCountRow>> GetContactLifecycleTransitionCountsAsync(CancellationToken cancellationToken = default) =>
        await _context.ContactLifecycleTransitions.AsNoTracking()
            .GroupBy(t => new { t.FromStage, t.ToStage })
            .Select(g => new LifecycleTransitionCountRow(g.Key.FromStage, g.Key.ToStage, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecentLifecycleChangeRow>> GetRecentLifecycleChangesAsync(int limit, CancellationToken cancellationToken = default) =>
        await (from t in _context.ContactLifecycleTransitions.AsNoTracking()
               join c in _context.Contacts.AsNoTracking() on t.ContactId equals c.Id
               orderby t.OccurredAt descending
               select new RecentLifecycleChangeRow(c.Id, c.FirstName, c.LastName, t.FromStage, t.ToStage, t.OccurredAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OnboardingStatusCountRow>> GetOnboardingCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        await _context.OnboardingRecords.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new OnboardingStatusCountRow(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MoneyByCurrency>> GetOnboardingAssociatedDealValueByCurrencyAsync(CancellationToken cancellationToken = default) =>
        await (from o in _context.OnboardingRecords.AsNoTracking()
               join d in _context.Deals.AsNoTracking() on o.DealId equals d.Id
               group d by d.Currency into g
               select new MoneyByCurrency(g.Key, g.Sum(d => d.Amount)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RecentOnboardingHandoffRow>> GetRecentOnboardingHandoffsAsync(int limit, CancellationToken cancellationToken = default) =>
        await (from o in _context.OnboardingRecords.AsNoTracking()
               join d in _context.Deals.AsNoTracking() on o.DealId equals d.Id
               join co in _context.Companies.AsNoTracking() on o.CompanyId equals co.Id
               join ct in _context.Contacts.AsNoTracking() on o.ContactId equals ct.Id into contactJoin
               from ct in contactJoin.DefaultIfEmpty()
               orderby o.CreatedAt descending
               select new RecentOnboardingHandoffRow(
                   o.Id, d.Id, d.Name, d.Amount, d.Currency, co.Name,
                   ct == null ? null : ct.FirstName, ct == null ? null : ct.LastName,
                   o.Status, o.CreatedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DealStageTransitionActivityRow>> GetRecentDealStageTransitionsAsync(int limit, CancellationToken cancellationToken = default) =>
        await (from t in _context.DealStageTransitions.AsNoTracking()
               join d in _context.Deals.AsNoTracking() on t.DealId equals d.Id
               orderby t.OccurredAt descending
               select new DealStageTransitionActivityRow(d.Id, d.Name, t.FromStage, t.ToStage, t.OccurredAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContactLifecycleTransitionActivityRow>> GetRecentContactLifecycleTransitionsAsync(int limit, CancellationToken cancellationToken = default) =>
        await (from t in _context.ContactLifecycleTransitions.AsNoTracking()
               join c in _context.Contacts.AsNoTracking() on t.ContactId equals c.Id
               orderby t.OccurredAt descending
               select new ContactLifecycleTransitionActivityRow(c.Id, c.FirstName, c.LastName, t.FromStage, t.ToStage, t.OccurredAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AutomationExecutionActivityRow>> GetRecentTerminalAutomationExecutionsAsync(int limit, CancellationToken cancellationToken = default) =>
        await _context.AutomationExecutions.AsNoTracking()
            .Where(a => a.Status == AutomationStatus.Succeeded || a.Status == AutomationStatus.Skipped || a.Status == AutomationStatus.Failed)
            .OrderByDescending(a => a.CompletedAt ?? a.StartedAt)
            .Select(a => new AutomationExecutionActivityRow(a.Id, a.AutomationType, a.EntityType, a.EntityId, a.Status, a.ResultSummary, a.CompletedAt ?? a.StartedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OnboardingActivityRow>> GetRecentOnboardingCreationsAsync(int limit, CancellationToken cancellationToken = default) =>
        await (from o in _context.OnboardingRecords.AsNoTracking()
               join d in _context.Deals.AsNoTracking() on o.DealId equals d.Id
               orderby o.CreatedAt descending
               select new OnboardingActivityRow(o.Id, d.Id, d.Name, o.CreatedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);
}
