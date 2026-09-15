using CrmIntegration.Application.Common;
using CrmIntegration.Application.Deals;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Sync.Services;

/// <summary>
/// Deals never perform duplicate/match search (per docs/SYNC_POLICY.md): there is no safe
/// deterministic key to dedupe deals by, so every unmapped deal is created — the only protection
/// against duplicates is the EntityMapping itself.
/// </summary>
public class DealSyncService : IDealSyncService
{
    private readonly IDealRepository _dealRepository;
    private readonly IEntityMappingRepository _mappingRepository;
    private readonly IDealHubSpotMapper _mapper;
    private readonly IHubSpotClient _hubSpotClient;
    private readonly ISyncJobExecutor _syncJobExecutor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DealSyncService> _logger;

    public DealSyncService(
        IDealRepository dealRepository,
        IEntityMappingRepository mappingRepository,
        IDealHubSpotMapper mapper,
        IHubSpotClient hubSpotClient,
        ISyncJobExecutor syncJobExecutor,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<DealSyncService> logger)
    {
        _dealRepository = dealRepository;
        _mappingRepository = mappingRepository;
        _mapper = mapper;
        _hubSpotClient = hubSpotClient;
        _syncJobExecutor = syncJobExecutor;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<SyncJob> SyncToHubSpotAsync(Guid dealId, string correlationId, CancellationToken cancellationToken = default) =>
        _syncJobExecutor.ExecuteAsync(EntityType.Deal, SyncDirection.InternalToHubSpot, dealId, null, correlationId,
            ct => SyncToHubSpotCoreAsync(dealId, ct), cancellationToken);

    public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default) =>
        _syncJobExecutor.ExecuteAsync(EntityType.Deal, SyncDirection.HubSpotToInternal, null, hubSpotId, correlationId,
            ct => SyncFromHubSpotCoreAsync(hubSpotId, ct), cancellationToken);

    private async Task<SyncActionResult> SyncToHubSpotCoreAsync(Guid dealId, CancellationToken cancellationToken)
    {
        var deal = await _dealRepository.GetByIdAsync(dealId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Deal), dealId);

        var mapping = await _mappingRepository.GetByInternalIdAsync(EntityType.Deal, dealId, cancellationToken: cancellationToken);
        var properties = _mapper.ToHubSpotProperties(deal);

        string? hubSpotId = null;
        var kind = SyncResultKind.MappedUpdate;

        if (mapping is not null)
        {
            _logger.LogInformation("MappingFound Deal InternalId={InternalId} ExternalId={ExternalId}", dealId, mapping.ExternalId);
            try
            {
                await _hubSpotClient.UpdateDealAsync(mapping.ExternalId, properties, cancellationToken);
                hubSpotId = mapping.ExternalId;
                _logger.LogInformation("HubSpotUpdate Deal ExternalId={ExternalId}", hubSpotId);
            }
            catch (HubSpotNotFoundException)
            {
                _logger.LogWarning("StaleMapping Deal InternalId={InternalId} ExternalId={ExternalId} — HubSpot object no longer exists, recreating (no dedupe key available for deals).", dealId, mapping.ExternalId);
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (hubSpotId is null)
        {
            var created = await _hubSpotClient.CreateDealAsync(properties, cancellationToken);
            hubSpotId = created.Id;
            kind = SyncResultKind.Created;
            _logger.LogInformation("HubSpotCreate Deal ExternalId={ExternalId}", hubSpotId);

            if (mapping is null)
            {
                mapping = new EntityMapping
                {
                    Id = Guid.NewGuid(),
                    EntityType = EntityType.Deal,
                    InternalId = dealId,
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

        if (deal.CompanyId is Guid companyId)
        {
            await SyncAssociationAsync(HubSpotObjectType.Deal, hubSpotId, HubSpotObjectType.Company, EntityType.Company, companyId, cancellationToken);
        }

        if (deal.ContactId is Guid contactId)
        {
            await SyncAssociationAsync(HubSpotObjectType.Deal, hubSpotId, HubSpotObjectType.Contact, EntityType.Contact, contactId, cancellationToken);
        }

        mapping!.LastSyncedAt = now;
        deal.HubSpotId = hubSpotId;
        deal.LastSyncedAt = now;
        deal.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SyncActionResult(kind, dealId, hubSpotId);
    }

    private async Task SyncAssociationAsync(
        HubSpotObjectType fromType, string fromHubSpotId, HubSpotObjectType toType, EntityType toEntityType, Guid toInternalId, CancellationToken cancellationToken)
    {
        var counterpartMapping = await _mappingRepository.GetByInternalIdAsync(toEntityType, toInternalId, cancellationToken: cancellationToken);
        if (counterpartMapping is null)
        {
            _logger.LogInformation("AssociationSkipped {FromType}->{ToType}: {ToEntityType} {ToInternalId} is not yet synced to HubSpot.", fromType, toType, toEntityType, toInternalId);
            return;
        }

        await _hubSpotClient.CreateAssociationAsync(fromType, fromHubSpotId, toType, counterpartMapping.ExternalId, cancellationToken);
        _logger.LogInformation("Associated {FromType} {FromHubSpotId} -> {ToType} {ToHubSpotId}", fromType, fromHubSpotId, toType, counterpartMapping.ExternalId);
    }

    private async Task<SyncActionResult> SyncFromHubSpotCoreAsync(string hubSpotId, CancellationToken cancellationToken)
    {
        var record = await _hubSpotClient.GetDealAsync(hubSpotId, _mapper.HubSpotProperties, cancellationToken)
            ?? throw new DomainValidationException($"HubSpot deal '{hubSpotId}' was not found.");

        var mapping = await _mappingRepository.GetByExternalIdAsync(EntityType.Deal, hubSpotId, cancellationToken: cancellationToken);
        Deal? deal = null;
        SyncResultKind kind;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (mapping is not null)
        {
            deal = await _dealRepository.GetByIdAsync(mapping.InternalId, cancellationToken);
            if (deal is null)
            {
                _logger.LogWarning("StaleMapping Deal ExternalId={ExternalId} InternalId={InternalId} — internal record no longer exists, recreating.", hubSpotId, mapping.InternalId);
            }
        }

        if (deal is not null)
        {
            _mapper.ApplyHubSpotProperties(deal, record);
            deal.UpdatedAt = now;
            deal.LastSyncedAt = now;
            kind = SyncResultKind.MappedUpdate;
            _logger.LogInformation("MappingFound Deal ExternalId={ExternalId} InternalId={InternalId}", hubSpotId, deal.Id);
        }
        else
        {
            deal = new Deal { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now };
            _mapper.ApplyHubSpotProperties(deal, record);
            await _dealRepository.AddAsync(deal, cancellationToken);
            kind = SyncResultKind.Imported;
            _logger.LogInformation("HubSpot object imported as new Deal {InternalId} (no dedupe key available for deals).", deal.Id);

            if (mapping is null)
            {
                mapping = new EntityMapping
                {
                    Id = Guid.NewGuid(),
                    EntityType = EntityType.Deal,
                    InternalId = deal.Id,
                    ExternalSystem = ExternalSystem.HubSpot,
                    ExternalId = hubSpotId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _mappingRepository.AddAsync(mapping, cancellationToken);
            }
            else
            {
                mapping.InternalId = deal.Id;
                mapping.UpdatedAt = now;
            }
        }

        await AttachAssociatedInternalIdAsync(hubSpotId, HubSpotObjectType.Company, EntityType.Company, cancellationToken, id => deal.CompanyId = id);
        await AttachAssociatedInternalIdAsync(hubSpotId, HubSpotObjectType.Contact, EntityType.Contact, cancellationToken, id => deal.ContactId = id);

        deal.HubSpotId = hubSpotId;
        mapping!.LastSyncedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SyncActionResult(kind, deal.Id, hubSpotId);
    }

    private async Task AttachAssociatedInternalIdAsync(
        string dealHubSpotId, HubSpotObjectType toType, EntityType toEntityType, CancellationToken cancellationToken, Action<Guid> assign)
    {
        var associatedIds = await _hubSpotClient.GetAssociatedIdsAsync(HubSpotObjectType.Deal, dealHubSpotId, toType, cancellationToken);
        if (associatedIds.Count == 0)
        {
            return;
        }

        var counterpartMapping = await _mappingRepository.GetByExternalIdAsync(toEntityType, associatedIds[0], cancellationToken: cancellationToken);
        if (counterpartMapping is not null)
        {
            assign(counterpartMapping.InternalId);
        }
        else
        {
            _logger.LogInformation("AssociationSkipped Deal->{ToType}: HubSpot record {ExternalId} is not yet synced internally.", toType, associatedIds[0]);
        }
    }
}
