using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class OnboardingRepository : IOnboardingRepository
{
    private readonly AppDbContext _context;

    public OnboardingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<OnboardingRecord?> GetByDealIdAsync(Guid dealId, CancellationToken cancellationToken = default) =>
        _context.OnboardingRecords.FirstOrDefaultAsync(o => o.DealId == dealId, cancellationToken);

    public Task<OnboardingRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.OnboardingRecords.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task AddAsync(OnboardingRecord record, CancellationToken cancellationToken = default) =>
        await _context.OnboardingRecords.AddAsync(record, cancellationToken);

    public async Task<IReadOnlyList<OnboardingRecord>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        await _context.OnboardingRecords.AsNoTracking().OrderByDescending(o => o.CreatedAt).Take(limit).ToListAsync(cancellationToken);
}
