using CrmIntegration.Application.Deals;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class DealRepository : IDealRepository
{
    private readonly AppDbContext _context;

    public DealRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Deals.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Deal>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Deals.AsNoTracking().OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);

    public async Task AddAsync(Deal deal, CancellationToken cancellationToken = default) =>
        await _context.Deals.AddAsync(deal, cancellationToken);
}
