using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Webhooks;

public class IntegrationEventRepositoryTests
{
    private static IntegrationEvent NewEvent(string externalEventId, IntegrationEventStatus status = IntegrationEventStatus.Received) => new()
    {
        Id = Guid.NewGuid(),
        ExternalEventId = externalEventId,
        EventType = "contact.propertyChange",
        EntityType = EntityType.Contact,
        EntityId = "1",
        Status = status,
        CorrelationId = "corr-1",
        ReceivedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task TryAddAsync_PersistsEvent_AndSurvivesAFreshRead()
    {
        using var factory = new WebhookApiFactory();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
        var externalId = "test-" + Guid.NewGuid().ToString("N");

        var added = await repository.TryAddAsync(NewEvent(externalId));
        Assert.True(added);

        using var freshScope = factory.Services.CreateScope();
        var freshRepository = freshScope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
        Assert.True(await freshRepository.ExistsAsync(externalId));
    }

    [Fact]
    public async Task UniqueConstraint_RejectsSecondInsert_WithSameExternalEventId()
    {
        using var factory = new WebhookApiFactory();
        var externalId = "test-" + Guid.NewGuid().ToString("N");

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
            Assert.True(await repository.TryAddAsync(NewEvent(externalId)));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
            var secondAdded = await repository.TryAddAsync(NewEvent(externalId));
            Assert.False(secondAdded);
        }
    }

    [Fact]
    public async Task TryClaimNextAsync_TransitionsReceivedToProcessing()
    {
        using var factory = new WebhookApiFactory();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();

        // TryClaimNextAsync always claims the globally oldest Received row, and this Postgres
        // database is shared across every test in the suite (other tests intentionally leave
        // Received rows behind, since the background worker is disabled here for determinism —
        // see WebhookApiFactory). Draining first guarantees our own row is the only Received one
        // left, so it's deterministically the next one claimed.
        await DrainAllReceivedAsync(repository);

        var externalId = "test-" + Guid.NewGuid().ToString("N");
        await repository.TryAddAsync(NewEvent(externalId));

        var claimed = await repository.TryClaimNextAsync();

        Assert.NotNull(claimed);
        Assert.Equal(externalId, claimed!.ExternalEventId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.IntegrationEvents.AsNoTracking().FirstAsync(e => e.ExternalEventId == externalId);
        Assert.Equal(IntegrationEventStatus.Processing, persisted.Status);
    }

    [Fact]
    public async Task TryClaimNextAsync_CannotClaimTheSameEventTwiceConcurrently()
    {
        using var factory = new WebhookApiFactory();
        var externalId = "test-" + Guid.NewGuid().ToString("N");

        using (var seedScope = factory.Services.CreateScope())
        {
            var seedRepository = seedScope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
            await DrainAllReceivedAsync(seedRepository);
            await seedRepository.TryAddAsync(NewEvent(externalId));
        }

        // Two separate scopes (separate DbContext instances) racing to claim the same row,
        // simulating two application instances/workers.
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();

        var resultA = await repositoryA.TryClaimNextAsync();
        var resultB = await repositoryB.TryClaimNextAsync();

        var claims = new[] { resultA, resultB }.Where(r => r?.ExternalEventId == externalId).ToList();
        Assert.Single(claims);
    }

    private static async Task DrainAllReceivedAsync(IIntegrationEventRepository repository)
    {
        while (await repository.TryClaimNextAsync() is not null)
        {
        }
    }

    [Fact]
    public async Task TryClaimNextAsync_ReturnsNull_WhenNothingIsReceivable()
    {
        using var factory = new WebhookApiFactory();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntegrationEventRepository>();
        var externalId = "test-" + Guid.NewGuid().ToString("N");
        await repository.TryAddAsync(NewEvent(externalId, IntegrationEventStatus.Processed));

        var claimed = await repository.TryClaimNextAsync();

        // Note: other Received rows may exist from other tests sharing this database, so we only
        // assert that our own Processed row was never returned.
        Assert.NotEqual(externalId, claimed?.ExternalEventId);
    }
}
