using CrmIntegration.Application.Automation;
using CrmIntegration.Application.Common;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CrmIntegration.Application.Sync.Services;

public class ContactSyncService : IContactSyncService
{
    private readonly IContactRepository _contactRepository;
    private readonly IEntityMappingRepository _mappingRepository;
    private readonly IContactHubSpotMapper _mapper;
    private readonly IContactMatchService _matchService;
    private readonly IHubSpotClient _hubSpotClient;
    private readonly ISyncJobExecutor _syncJobExecutor;
    private readonly IContactLifecycleAutomationService _lifecycleAutomationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContactSyncService> _logger;

    public ContactSyncService(
        IContactRepository contactRepository,
        IEntityMappingRepository mappingRepository,
        IContactHubSpotMapper mapper,
        IContactMatchService matchService,
        IHubSpotClient hubSpotClient,
        ISyncJobExecutor syncJobExecutor,
        IContactLifecycleAutomationService lifecycleAutomationService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ContactSyncService> logger)
    {
        _contactRepository = contactRepository;
        _mappingRepository = mappingRepository;
        _mapper = mapper;
        _matchService = matchService;
        _hubSpotClient = hubSpotClient;
        _syncJobExecutor = syncJobExecutor;
        _lifecycleAutomationService = lifecycleAutomationService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task<SyncJob> SyncToHubSpotAsync(Guid contactId, string correlationId, CancellationToken cancellationToken = default) =>
        _syncJobExecutor.ExecuteAsync(EntityType.Contact, SyncDirection.InternalToHubSpot, contactId, null, correlationId,
            ct => SyncToHubSpotCoreAsync(contactId, ct), cancellationToken);

    public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default, TransitionSource source = TransitionSource.HubSpotSync) =>
        _syncJobExecutor.ExecuteAsync(EntityType.Contact, SyncDirection.HubSpotToInternal, null, hubSpotId, correlationId,
            ct => SyncFromHubSpotCoreAsync(hubSpotId, correlationId, source, ct), cancellationToken);

    private async Task<SyncActionResult> SyncToHubSpotCoreAsync(Guid contactId, CancellationToken cancellationToken)
    {
        var contact = await _contactRepository.GetByIdAsync(contactId, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(Contact), contactId);

        var mapping = await _mappingRepository.GetByInternalIdAsync(EntityType.Contact, contactId, cancellationToken: cancellationToken);
        var properties = _mapper.ToHubSpotProperties(contact);

        string? hubSpotId = null;
        var kind = SyncResultKind.MappedUpdate;

        if (mapping is not null)
        {
            _logger.LogInformation("MappingFound Contact InternalId={InternalId} ExternalId={ExternalId}", contactId, mapping.ExternalId);
            try
            {
                await _hubSpotClient.UpdateContactAsync(mapping.ExternalId, properties, cancellationToken);
                hubSpotId = mapping.ExternalId;
                _logger.LogInformation("HubSpotUpdate Contact ExternalId={ExternalId}", hubSpotId);
            }
            catch (HubSpotNotFoundException)
            {
                _logger.LogWarning("StaleMapping Contact InternalId={InternalId} ExternalId={ExternalId} — HubSpot object no longer exists, recovering.", contactId, mapping.ExternalId);
            }
        }

        if (hubSpotId is null)
        {
            var match = await _matchService.FindHubSpotMatchAsync(contact.Email, cancellationToken);
            if (match.MatchType == MatchType.Ambiguous)
            {
                throw new SyncAmbiguousMatchException(EntityType.Contact, $"Multiple HubSpot contacts match Contact {contactId}; refusing to guess.");
            }

            if (match.MatchType == MatchType.ExactMatch)
            {
                hubSpotId = match.HubSpotId!;
                await _hubSpotClient.UpdateContactAsync(hubSpotId, properties, cancellationToken);
                kind = SyncResultKind.ReusedExistingMatch;
                _logger.LogInformation("ExactMatchFound Contact HubSpotId={ExternalId}; reusing instead of creating a duplicate.", hubSpotId);
            }
            else
            {
                var created = await _hubSpotClient.CreateContactAsync(properties, cancellationToken);
                hubSpotId = created.Id;
                kind = SyncResultKind.Created;
                _logger.LogInformation("HubSpotCreate Contact ExternalId={ExternalId}", hubSpotId);
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (mapping is null)
            {
                mapping = new EntityMapping
                {
                    Id = Guid.NewGuid(),
                    EntityType = EntityType.Contact,
                    InternalId = contactId,
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

        if (contact.CompanyId is Guid companyId)
        {
            await SyncCompanyAssociationAsync(hubSpotId, companyId, cancellationToken);
        }

        var syncedAt = _timeProvider.GetUtcNow().UtcDateTime;
        mapping!.LastSyncedAt = syncedAt;
        contact.HubSpotId = hubSpotId;
        contact.LastSyncedAt = syncedAt;
        contact.UpdatedAt = syncedAt;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SyncActionResult(kind, contactId, hubSpotId);
    }

    private async Task SyncCompanyAssociationAsync(string contactHubSpotId, Guid companyId, CancellationToken cancellationToken)
    {
        var companyMapping = await _mappingRepository.GetByInternalIdAsync(EntityType.Company, companyId, cancellationToken: cancellationToken);
        if (companyMapping is null)
        {
            _logger.LogInformation(
                "AssociationSkipped Contact->Company: Company {CompanyId} is not yet synced to HubSpot.", companyId);
            return;
        }

        await _hubSpotClient.CreateAssociationAsync(
            HubSpotObjectType.Contact, contactHubSpotId, HubSpotObjectType.Company, companyMapping.ExternalId, cancellationToken);
        _logger.LogInformation("Associated Contact {ContactHubSpotId} -> Company {CompanyHubSpotId}", contactHubSpotId, companyMapping.ExternalId);
    }

    private async Task<SyncActionResult> SyncFromHubSpotCoreAsync(string hubSpotId, string correlationId, TransitionSource source, CancellationToken cancellationToken)
    {
        var record = await _hubSpotClient.GetContactAsync(hubSpotId, _mapper.HubSpotProperties, cancellationToken)
            ?? throw new DomainValidationException($"HubSpot contact '{hubSpotId}' was not found.");

        var mapping = await _mappingRepository.GetByExternalIdAsync(EntityType.Contact, hubSpotId, cancellationToken: cancellationToken);
        Contact? contact = null;
        SyncResultKind kind;
        LifecycleStage? previousLifecycleStage = null;

        if (mapping is not null)
        {
            contact = await _contactRepository.GetByIdAsync(mapping.InternalId, cancellationToken);
            if (contact is null)
            {
                _logger.LogWarning("StaleMapping Contact ExternalId={ExternalId} InternalId={InternalId} — internal record no longer exists, recovering.", hubSpotId, mapping.InternalId);
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (contact is not null)
        {
            previousLifecycleStage = contact.LifecycleStage;
            _mapper.ApplyHubSpotProperties(contact, record);
            contact.UpdatedAt = now;
            contact.LastSyncedAt = now;
            kind = SyncResultKind.MappedUpdate;
            _logger.LogInformation("MappingFound Contact ExternalId={ExternalId} InternalId={InternalId}", hubSpotId, contact.Id);
        }
        else
        {
            var normalizedEmail = Normalization.NormalizeEmail(record.Properties.GetValueOrDefault("email") ?? string.Empty);
            if (string.IsNullOrEmpty(normalizedEmail))
            {
                throw new DomainValidationException($"HubSpot contact '{hubSpotId}' has no email; cannot import (email is required internally).");
            }

            var match = await _matchService.FindInternalMatchAsync(normalizedEmail, cancellationToken);
            if (match.MatchType == MatchType.Ambiguous)
            {
                throw new SyncAmbiguousMatchException(EntityType.Contact, $"Multiple internal contacts match HubSpot contact {hubSpotId}; refusing to guess.");
            }

            if (match.MatchType == MatchType.ExactMatch)
            {
                contact = await _contactRepository.GetByIdAsync(match.InternalId!.Value, cancellationToken)
                    ?? throw new EntityNotFoundException(nameof(Contact), match.InternalId.Value);
                _mapper.ApplyHubSpotProperties(contact, record);
                contact.UpdatedAt = now;
                kind = SyncResultKind.ReusedExistingMatch;
                _logger.LogInformation("ExactMatchFound Contact InternalId={InternalId}; attaching instead of importing a duplicate.", contact.Id);
            }
            else
            {
                contact = new Contact { Id = Guid.NewGuid(), CreatedAt = now, UpdatedAt = now };
                _mapper.ApplyHubSpotProperties(contact, record);
                await _contactRepository.AddAsync(contact, cancellationToken);
                kind = SyncResultKind.Imported;
                _logger.LogInformation("HubSpot object imported as new Contact {InternalId}", contact.Id);
            }

            if (mapping is null)
            {
                mapping = new EntityMapping
                {
                    Id = Guid.NewGuid(),
                    EntityType = EntityType.Contact,
                    InternalId = contact.Id,
                    ExternalSystem = ExternalSystem.HubSpot,
                    ExternalId = hubSpotId,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                await _mappingRepository.AddAsync(mapping, cancellationToken);
            }
            else
            {
                mapping.InternalId = contact.Id;
                mapping.UpdatedAt = now;
            }
        }

        // Attach to an internal Company only if that company is already mapped and the HubSpot
        // side actually has a company association — never guess a CompanyId out of thin air.
        var associatedCompanyIds = await _hubSpotClient.GetAssociatedIdsAsync(HubSpotObjectType.Contact, hubSpotId, HubSpotObjectType.Company, cancellationToken);
        if (associatedCompanyIds.Count > 0)
        {
            var companyMapping = await _mappingRepository.GetByExternalIdAsync(EntityType.Company, associatedCompanyIds[0], cancellationToken: cancellationToken);
            if (companyMapping is not null)
            {
                contact.CompanyId = companyMapping.InternalId;
            }
            else
            {
                _logger.LogInformation("AssociationSkipped Company->Contact: HubSpot company {ExternalId} is not yet synced internally.", associatedCompanyIds[0]);
            }
        }

        contact.HubSpotId = hubSpotId;
        mapping!.LastSyncedAt = now;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _lifecycleAutomationService.EvaluateAsync(contact, previousLifecycleStage, source, correlationId, cancellationToken: cancellationToken);

        return new SyncActionResult(kind, contact.Id, hubSpotId);
    }
}
