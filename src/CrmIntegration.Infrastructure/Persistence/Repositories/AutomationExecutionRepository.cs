using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class AutomationExecutionRepository : IAutomationExecutionRepository
{
    private readonly AppDbContext _context;

    public AutomationExecutionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AutomationExecution execution, CancellationToken cancellationToken = default) =>
        await _context.AutomationExecutions.AddAsync(execution, cancellationToken);

    public Task<AutomationExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.AutomationExecutions.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<AutomationExecution?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.AutomationExecutions.FirstOrDefaultAsync(a => a.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<IReadOnlyList<AutomationExecution>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        await _context.AutomationExecutions.AsNoTracking().OrderByDescending(a => a.StartedAt).Take(limit).ToListAsync(cancellationToken);
}
