using CrmIntegration.Application.Common;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Sync.Services;

public class CompanySyncService : ICompanySyncService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IEntityMappingRepository _mappingRepository;
    private readonly ICompanyHubSpotMapper _mapper;
    private readonly ICompanyMatchService _matchService;
    private readonly IHubSpotClient _hubSpotClient;
    private readonly ISyncJobExecutor _syncJobExecutor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CompanySyncService> _logger;

    public CompanySyncService(
        ICompanyRepository companyRepository,
        IEntityMappingRepository mappingRepository,
        ICompanyHubSpotMapper mapper,
        ICompanyMatchService matchService,
        IHubSpotClient hubSpotClient,
        ISyncJobExecutor syncJobExecutor,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<CompanySyncService> logger)
    {
        _companyRepository = companyRepository;
        _mappingRepository = mappingRepository;
        _mapper = mapper;
        _matchService = matchService;
        _hubSpotClient = hubSpotClient;
        _syncJobExecutor = syncJobExecutor;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<SyncJob> SyncToHubSpotAsync(Guid companyId, string correlationId, CancellationToken cancellationToken = default) =>
        _syncJobExecutor.ExecuteAsync(EntityType.Company, SyncDirection.InternalToHubSpot, companyId, null, correlationId,
            ct => SyncToHubSpotCoreAsync(companyId, ct), cancellationToken);

    public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default) =>
        _syncJobExecutor.ExecuteAsync(EntityType.Company, SyncDirection.HubSpotToInternal, null, hubSpotId, correlationId,
            ct => SyncFromHubSpotCoreAsync(hubSpotId, ct), cancellationToken);

    private async Task<SyncActionResult> SyncToHubSpotCoreAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Company), companyId);

        var mapping = await _mappingRepository.GetByInternalIdAsync(EntityType.Company, companyId, cancellationToken: cancellationToken);
        var properties = _mapper.ToHubSpotProperties(company);

        string? hubSpotId = null;
        var kind = SyncResultKind.MappedUpdate;

        if (mapping is not null)
        {
            _logger.LogInformation("MappingFound Company InternalId={InternalId} ExternalId={ExternalId}", companyId, mapping.ExternalId);
            try
            {
                await _hubSpotClient.UpdateCompanyAsync(mapping.ExternalId, properties, cancellationToken);
                hubSpotId = mapping.ExternalId;
                kind = SyncResultKind.MappedUpdate;
                _logger.LogInformation("HubSpotUpdate Company ExternalId={ExternalId}", hubSpotId);
            }
            catch (HubSpotNotFoundException)
            {
                _logger.LogWarning("StaleMapping Company InternalId={InternalId} ExternalId={ExternalId} — HubSpot object no longer exists, recovering.", companyId, mapping.ExternalId);
            }
        }

        if (hubSpotId is null)
        {
            var match = await _matchService.FindHubSpotMatchAsync(company.Domain, Normalization.NormalizeCompanyName(company.Name), cancellationToken);
            if (match.MatchType == MatchType.Ambiguous)
            {
                throw new SyncAmbiguousMatchException(EntityType.Company, $"Multiple HubSpot companies match Company {companyId}; refusing to guess.");
            }

            if (match.MatchType == MatchType.ExactMatch)
            {
                hubSpotId = match.HubSpotId!;
                await _hubSpotClient.UpdateCompanyAsync(hubSpotId, properties, cancellationToken);
                kind = SyncResultKind.ReusedExistingMatch;
                _logger.LogInformation("ExactMatchFound Company HubSpotId={ExternalId}; reusing instead of creating a duplicate.", hubSpotId);
            }
            else
            {
                var created = await _hubSpotClient.CreateCompanyAsync(properties, cancellationToken);
                hubSpotId = created.Id;
                kind = SyncResultKind.Created;
                _logger.LogInformation("HubSpotCreate Company ExternalId={ExternalId}", hubSpotId);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (mapping is null)
            {
                mapping = new EntityMapping
                {
                    Id = Guid.NewGuid(),
                    EntityType = EntityType.Company,
                    InternalId = companyId,
                    ExternalSystem = ExternalSystem.HubSpot,
                    ExternalId = hubSpotId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _mappingRepository.AddAsync(mapping, cancellationToken);
            }
            else
            {
                mapping.ExternalId = hubSpotId;
                mapping.UpdatedAt = now;
            }
        }

        var syncedAt = _timeProvider.GetUtcNow().UtcDateTime;
        mapping!.LastSyncedAt = syncedAt;
        company.HubSpotId = hubSpotId;
        company.LastSyncedAt = syncedAt;
        company.UpdatedAt = syncedAt;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SyncActionResult(kind, companyId, hubSpotId);
    }

    private async Task<SyncActionResult> SyncFromHubSpotCoreAsync(string hubSpotId, CancellationToken cancellationToken)
    {
        var record = await _hubSpotClient.GetCompanyAsync(hubSpotId, _mapper.HubSpotProperties, cancellationToken)
            ?? throw new DomainValidationException($"HubSpot company '{hubSpotId}' was not found.");

        var mapping = await _mappingRepository.GetByExternalIdAsync(EntityType.Company, hubSpotId, cancellationToken: cancellationToken);
        Company? company = null;
        SyncResultKind kind;

        if (mapping is not null)
        {
            company = await _companyRepository.GetByIdAsync(mapping.InternalId, cancellationToken);
            if (company is null)
            {
                _logger.LogWarning("StaleMapping Company ExternalId={ExternalId} InternalId={InternalId} — internal record no longer exists, recovering.", hubSpotId, mapping.InternalId);
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (company is not null)
        {
            _mapper.ApplyHubSpotProperties(company, record);
            company.UpdatedAt = now;
            company.LastSyncedAt = now;
            kind = SyncResultKind.MappedUpdate;
            _logger.LogInformation("MappingFound Company ExternalId={ExternalId} InternalId={InternalId}", hubSpotId, company.Id);
        }
        else
        {
            var normalizedDomain = Normalization.NormalizeDomain(record.Properties.GetValueOrDefault("domain"));
            var normalizedName = Normalization.NormalizeCompanyName(record.Properties.GetValueOrDefault("name") ?? "Unnamed Company");
            var match = await _matchService.FindInternalMatchAsync(normalizedDomain, normalizedName, cancellationToken);

            if (match.MatchType == MatchType.Ambiguous)
            {
                throw new SyncAmbiguousMatchException(EntityType.Company, $"Multiple internal companies match HubSpot company {hubSpotId}; refusing to guess.");
            }

            if (match.MatchType == MatchType.ExactMatch)
            {
                company = await _companyRepository.GetByIdAsync(match.InternalId!.Value, cancellationToken)
                    ?? throw new EntityNotFoundException(nameof(Company), match.InternalId.Value);
                _mapper.ApplyHubSpotProperties(company, record);
                company.UpdatedAt = now;
                kind = SyncResultKind.ReusedExistingMatch;
                _logger.LogInformation("ExactMatchFound Company InternalId={InternalId}; attaching instead of importing a duplicate.", company.Id);
            }
            else
            {
                company = new Company { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now };
                _mapper.ApplyHubSpotProperties(company, record);
                await _companyRepository.AddAsync(company, cancellationToken);
                kind = SyncResultKind.Imported;
                _logger.LogInformation("HubSpot object imported as new Company {InternalId}", company.Id);
            }

            if (mapping is null)
            {
                mapping = new EntityMapping
                {
                    Id = Guid.NewGuid(),
                    EntityType = EntityType.Company,
                    InternalId = company.Id,
                    ExternalSystem = ExternalSystem.HubSpot,
                    ExternalId = hubSpotId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _mappingRepository.AddAsync(mapping, cancellationToken);
            }
            else
            {
                mapping.InternalId = company.Id;
                mapping.UpdatedAt = now;
            }
        }

        company.HubSpotId = hubSpotId;
        mapping!.LastSyncedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SyncActionResult(kind, company.Id, hubSpotId);
    }
}
