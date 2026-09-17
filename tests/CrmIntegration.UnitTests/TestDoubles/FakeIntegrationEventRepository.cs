using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.UnitTests.TestDoubles;

public class FakeIntegrationEventRepository : IIntegrationEventRepository
{
    public List<IntegrationEvent> Events { get; } = new();

    public Task<bool> TryAddAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        if (Events.Any(e => e.ExternalEventId == integrationEvent.ExternalEventId))
        {
            return Task.FromResult(false);
        }

        Events.Add(integrationEvent);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string externalEventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Events.Any(e => e.ExternalEventId == externalEventId));

    public Task<IntegrationEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Events.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<IntegrationEvent>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IntegrationEvent>>(Events.OrderByDescending(e => e.ReceivedAt).Take(limit).ToList());

    public Task<IntegrationEvent?> TryClaimNextAsync(CancellationToken cancellationToken = default)
    {
        var next = Events
            .Where(e => e.Status == IntegrationEventStatus.Received)
            .OrderBy(e => e.ReceivedAt)
            .FirstOrDefault();

        if (next is null)
        {
            return Task.FromResult<IntegrationEvent?>(null);
        }

        next.Status = IntegrationEventStatus.Processing;
        return Task.FromResult<IntegrationEvent?>(next);
    }
}
