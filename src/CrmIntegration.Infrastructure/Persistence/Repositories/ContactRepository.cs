using CrmIntegration.Application.Contacts;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class ContactRepository : IContactRepository
{
    private readonly AppDbContext _context;

    public ContactRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Contacts.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Contacts.AsNoTracking().OrderBy(c => c.LastName).ToListAsync(cancellationToken);

    public async Task AddAsync(Contact contact, CancellationToken cancellationToken = default) =>
        await _context.Contacts.AddAsync(contact, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Contacts.AnyAsync(c => c.Id == id, cancellationToken);
}
