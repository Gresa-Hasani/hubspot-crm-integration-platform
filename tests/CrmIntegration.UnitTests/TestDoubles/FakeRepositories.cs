using CrmIntegration.Application.Common;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

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

    public Task<IReadOnlyList<Company>> FindByNormalizedDomainAsync(string normalizedDomain, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Company>>(Companies.Where(c => c.Domain == normalizedDomain).ToList());

    public Task<IReadOnlyList<Company>> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Company>>(Companies.Where(c => c.Name == normalizedName).ToList());
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

    public Task<IReadOnlyList<Contact>> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Contact>>(Contacts.Where(c => c.Email == normalizedEmail).ToList());
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

    public Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default) =>
        operation(cancellationToken);
}

public class FakeEntityMappingRepository : IEntityMappingRepository
{
    public List<EntityMapping> Mappings { get; } = new();

    public Task<EntityMapping?> GetByInternalIdAsync(EntityType entityType, Guid internalId, ExternalSystem externalSystem = ExternalSystem.HubSpot, CancellationToken cancellationToken = default) =>
        Task.FromResult(Mappings.FirstOrDefault(m => m.EntityType == entityType && m.InternalId == internalId && m.ExternalSystem == externalSystem));

    public Task<EntityMapping?> GetByExternalIdAsync(EntityType entityType, string externalId, ExternalSystem externalSystem = ExternalSystem.HubSpot, CancellationToken cancellationToken = default) =>
        Task.FromResult(Mappings.FirstOrDefault(m => m.EntityType == entityType && m.ExternalId == externalId && m.ExternalSystem == externalSystem));

    public Task AddAsync(EntityMapping mapping, CancellationToken cancellationToken = default)
    {
        Mappings.Add(mapping);
        return Task.CompletedTask;
    }
}

public class FakeSyncJobRepository : ISyncJobRepository
{
    public List<SyncJob> Jobs { get; } = new();

    public Task AddAsync(SyncJob job, CancellationToken cancellationToken = default)
    {
        Jobs.Add(job);
        return Task.CompletedTask;
    }

    public Task<SyncJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Jobs.FirstOrDefault(j => j.Id == id));

    public Task<IReadOnlyList<SyncJob>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SyncJob>>(Jobs.OrderByDescending(j => j.StartedAt).Take(limit).ToList());
}

public class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLog> Entries { get; } = new();

    public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
