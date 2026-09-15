using CrmIntegration.Application.Companies;
using CrmIntegration.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegration.Infrastructure.Persistence.Repositories;

public class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _context;

    public CompanyRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default) =>
        await _context.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(cancellationToken);

    public async Task AddAsync(Company company, CancellationToken cancellationToken = default) =>
        await _context.Companies.AddAsync(company, cancellationToken);

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Companies.AnyAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Company>> FindByNormalizedDomainAsync(string normalizedDomain, CancellationToken cancellationToken = default) =>
        await _context.Companies.Where(c => c.Domain == normalizedDomain).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Company>> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default) =>
        await _context.Companies.Where(c => c.Name == normalizedName).ToListAsync(cancellationToken);
}
