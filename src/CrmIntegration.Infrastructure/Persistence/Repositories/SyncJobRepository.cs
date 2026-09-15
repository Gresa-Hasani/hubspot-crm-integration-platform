using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class SyncJobRepository : ISyncJobRepository
{
    private readonly AppDbContext _context;

    public SyncJobRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SyncJob job, CancellationToken cancellationToken = default) =>
        await _context.SyncJobs.AddAsync(job, cancellationToken);

    public Task<SyncJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.SyncJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SyncJob>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        await _context.SyncJobs.AsNoTracking().OrderByDescending(j => j.StartedAt).Take(limit).ToListAsync(cancellationToken);
}
