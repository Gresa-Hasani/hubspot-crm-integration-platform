using CrmIntegration.Application.Common;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Domain.Entities;

namespace CrmIntegration.UnitTests.TestDoubles;

public class FakeCompanyRepository : ICompanyRepository
{
    public List<Company> Companies { get; } = new();

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Companies.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Company>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Company>>(Companies.ToList());

    public Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        Companies.Add(company);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Companies.Any(c => c.Id == id));
}

public class FakeContactRepository : IContactRepository
{
    public List<Contact> Contacts { get; } = new();

    public Task<Contact?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Contacts.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Contact>>(Contacts.ToList());

    public Task AddAsync(Contact contact, CancellationToken cancellationToken = default)
    {
        Contacts.Add(contact);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Contacts.Any(c => c.Id == id));
}

public class FakeDealRepository : IDealRepository
{
    public List<Deal> Deals { get; } = new();

    public Task<Deal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Deals.FirstOrDefault(d => d.Id == id));

    public Task<IReadOnlyList<Deal>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Deal>>(Deals.ToList());

    public Task AddAsync(Deal deal, CancellationToken cancellationToken = default)
    {
        Deals.Add(deal);
        return Task.CompletedTask;
    }
}

public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
