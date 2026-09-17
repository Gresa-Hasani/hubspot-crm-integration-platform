using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class ContactLifecycleTransitionRepository : IContactLifecycleTransitionRepository
{
    private readonly AppDbContext _context;

    public ContactLifecycleTransitionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ContactLifecycleTransition transition, CancellationToken cancellationToken = default) =>
        await _context.ContactLifecycleTransitions.AddAsync(transition, cancellationToken);

    public async Task<IReadOnlyList<ContactLifecycleTransition>> ListByContactAsync(Guid contactId, CancellationToken cancellationToken = default) =>
        await _context.ContactLifecycleTransitions.AsNoTracking()
            .Where(t => t.ContactId == contactId)
            .OrderBy(t => t.OccurredAt)
            .ToListAsync(cancellationToken);
}
