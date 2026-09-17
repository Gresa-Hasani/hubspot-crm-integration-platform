using CrmIntegration.Domain.Entities;

namespace CrmIntegration.Application.Webhooks;

/// <summary>
/// Processes exactly one already-claimed IntegrationEvent by translating it into the correct
/// Phase 4 HubSpot -> Internal sync call. Contains no mapping, duplicate-detection, association,
/// or EntityMapping logic of its own — Phase 4's sync services remain the single source of truth
/// for all of that (see docs/WEBHOOKS.md "Processor -> Phase 4 integration").
/// </summary>
public interface IIntegrationEventProcessor
{
    Task ProcessAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
