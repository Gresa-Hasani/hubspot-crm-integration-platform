using CrmIntegration.Application.Sync;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Application.Sync.Services;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Sync.Services;

public class CompanySyncServiceTests
{
    private readonly FakeCompanyRepository _companyRepository = new();
    private readonly FakeEntityMappingRepository _mappingRepository = new();
    private readonly FakeSyncJobRepository _syncJobRepository = new();
    private readonly FakeAuditLogRepository _auditLogRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private CompanySyncService CreateService(FakeHubSpotClient hubSpotClient)
    {
        var mapper = new CompanyHubSpotMapper();
        var matchService = new CompanyMatchService(_companyRepository, hubSpotClient);
        var retryExecutor = new HubSpotRetryExecutor(new InstantRetryDelayProvider(), Options.Create(new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }), NullLogger<HubSpotRetryExecutor>.Instance);
        var jobExecutor = new SyncJobExecutor(_syncJobRepository, _auditLogRepository, retryExecutor, _unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), Options.Create(new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }), NullLogger<SyncJobExecutor>.Instance);

        return new CompanySyncService(_companyRepository, _mappingRepository, mapper, matchService, hubSpotClient, jobExecutor, _unitOfWork, new FixedTimeProvider(DateTimeOffset.UtcNow), NullLogger<CompanySyncService>.Instance);
    }

    private Company AddCompany(string name = "Acme", string? domain = "acme.example")
    {
        var company = new Company { Id = Guid.NewGuid(), Name = name, Domain = domain, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _companyRepository.Companies.Add(company);
        return company;
    }

    [Fact]
    public async Task SyncToHubSpotAsync_CreatesHubSpotCompany_WhenUnmapped()
    {
        var hubSpot = new FakeHubSpotClient();
        var company = AddCompany();
        var service = CreateService(hubSpot);

        var job = await service.SyncToHubSpotAsync(company.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.Equal(1, hubSpot.CreateCallCount);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_IsIdempotent_NoDuplicateOnSecondSync()
    {
        var hubSpot = new FakeHubSpotClient();
        var company = AddCompany();
        var service = CreateService(hubSpot);

        await service.SyncToHubSpotAsync(company.Id, "corr-1");
        await service.SyncToHubSpotAsync(company.Id, "corr-2");

        Assert.Equal(1, hubSpot.CreateCallCount);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_ReusesExactDomainMatch()
    {
        var hubSpot = new FakeHubSpotClient();
        var existing = await hubSpot.CreateCompanyAsync(new Dictionary<string, string?> { ["domain"] = "acme.example", ["name"] = "Acme" });
        var company = AddCompany();
        var service = CreateService(hubSpot);

        var job = await service.SyncToHubSpotAsync(company.Id, "corr-1");

        Assert.Equal(existing.Id, job.ExternalId);
        Assert.Equal(1, hubSpot.CreateCallCount);
    }

    [Fact]
    public async Task SyncToHubSpotAsync_RecoversFromStaleMapping()
    {
        var hubSpot = new FakeHubSpotClient();
        var company = AddCompany();
        _mappingRepository.Mappings.Add(new EntityMapping
        {
            Id = Guid.NewGuid(), EntityType = EntityType.Company, InternalId = company.Id,
            ExternalSystem = ExternalSystem.HubSpot, ExternalId = "gone", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        var service = CreateService(hubSpot);

        var job = await service.SyncToHubSpotAsync(company.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.NotEqual("gone", job.ExternalId);
        Assert.Single(_mappingRepository.Mappings);
    }

    [Fact]
    public async Task SyncFromHubSpotAsync_ImportsNewCompany_WhenUnmapped()
    {
        var hubSpot = new FakeHubSpotClient();
        var record = await hubSpot.CreateCompanyAsync(new Dictionary<string, string?> { ["name"] = "Globex", ["domain"] = "globex.example" });
        var service = CreateService(hubSpot);

        var job = await service.SyncFromHubSpotAsync(record.Id, "corr-1");

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.Single(_companyRepository.Companies);
        Assert.Equal("globex.example", _companyRepository.Companies[0].Domain);
    }

    [Fact]
    public async Task SyncFromHubSpotAsync_IsIdempotent_SecondImportDoesNotDuplicate()
    {
        var hubSpot = new FakeHubSpotClient();
        var record = await hubSpot.CreateCompanyAsync(new Dictionary<string, string?> { ["name"] = "Globex", ["domain"] = "globex.example" });
        var service = CreateService(hubSpot);

        await service.SyncFromHubSpotAsync(record.Id, "corr-1");
        await service.SyncFromHubSpotAsync(record.Id, "corr-2");

        Assert.Single(_companyRepository.Companies);
        Assert.Single(_mappingRepository.Mappings);
    }
}
