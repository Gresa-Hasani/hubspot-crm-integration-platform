using CrmIntegration.Application.Sync.Services;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.UnitTests.TestDoubles;

/// <summary>
/// Configurable fakes for the Phase 4 sync services, used to test the Phase 5 event processor's
/// routing/outcome-handling in isolation — the processor must never re-implement Phase 4 logic,
/// so these fakes just return whatever the test configures.
/// </summary>
public class FakeContactSyncService : IContactSyncService
{
    public Func<string, string, SyncJob>? OnSyncFromHubSpot { get; set; }
    public int CallCount { get; private set; }

    public Task<SyncJob> SyncToHubSpotAsync(Guid contactId, string correlationId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default, TransitionSource source = TransitionSource.HubSpotSync)
    {
        CallCount++;
        return Task.FromResult(OnSyncFromHubSpot!(hubSpotId, correlationId));
    }
}

public class FakeCompanySyncService : ICompanySyncService
{
    public Func<string, string, SyncJob>? OnSyncFromHubSpot { get; set; }
    public int CallCount { get; private set; }

    public Task<SyncJob> SyncToHubSpotAsync(Guid companyId, string correlationId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(OnSyncFromHubSpot!(hubSpotId, correlationId));
    }
}

public class FakeDealSyncService : IDealSyncService
{
    public Func<string, string, SyncJob>? OnSyncFromHubSpot { get; set; }
    public int CallCount { get; private set; }

    public Task<SyncJob> SyncToHubSpotAsync(Guid dealId, string correlationId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<SyncJob> SyncFromHubSpotAsync(string hubSpotId, string correlationId, CancellationToken cancellationToken = default, TransitionSource source = TransitionSource.HubSpotSync)
    {
        CallCount++;
        return Task.FromResult(OnSyncFromHubSpot!(hubSpotId, correlationId));
    }
}
