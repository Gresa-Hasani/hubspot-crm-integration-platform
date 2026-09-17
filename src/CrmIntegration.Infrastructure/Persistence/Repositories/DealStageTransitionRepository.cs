using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class DealStageTransitionRepository : IDealStageTransitionRepository
{
    private readonly AppDbContext _context;

    public DealStageTransitionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DealStageTransition transition, CancellationToken cancellationToken = default) =>
        await _context.DealStageTransitions.AddAsync(transition, cancellationToken);

    public async Task<IReadOnlyList<DealStageTransition>> ListByDealAsync(Guid dealId, CancellationToken cancellationToken = default) =>
        await _context.DealStageTransitions.AsNoTracking()
            .Where(t => t.DealId == dealId)
            .OrderBy(t => t.OccurredAt)
            .ToListAsync(cancellationToken);
}
