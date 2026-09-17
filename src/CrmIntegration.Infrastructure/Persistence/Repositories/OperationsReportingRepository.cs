using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class OperationsReportingRepository : IOperationsReportingRepository
{
    private readonly AppDbContext _context;

    public OperationsReportingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StatusCountMetric>> GetSyncJobCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        await _context.SyncJobs.AsNoTracking()
            .GroupBy(j => j.Status)
            .Select(g => new StatusCountMetric(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StatusCountMetric>> GetIntegrationEventCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        await _context.IntegrationEvents.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new StatusCountMetric(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StatusCountMetric>> GetAutomationExecutionCountsByStatusAsync(CancellationToken cancellationToken = default) =>
        await _context.AutomationExecutions.AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new StatusCountMetric(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

    public Task<int> CountSyncJobFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        _context.SyncJobs.AsNoTracking()
            .Where(j => (j.Status == SyncStatus.Failed || j.Status == SyncStatus.DeadLettered) && j.StartedAt >= sinceUtc)
            .CountAsync(cancellationToken);

    public Task<int> CountIntegrationEventFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        _context.IntegrationEvents.AsNoTracking()
            .Where(e => (e.Status == IntegrationEventStatus.Failed || e.Status == IntegrationEventStatus.DeadLettered) && e.ReceivedAt >= sinceUtc)
            .CountAsync(cancellationToken);

    public Task<int> CountAutomationFailuresSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default) =>
        _context.AutomationExecutions.AsNoTracking()
            .Where(a => a.Status == AutomationStatus.Failed && a.StartedAt >= sinceUtc)
            .CountAsync(cancellationToken);
}
