using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Webhooks;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.Webhooks;

public class WebhookIngestionServiceTests
{
    private readonly FakeIntegrationEventRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private WebhookIngestionService CreateService() =>
        new(new WebhookPayloadParser(), _repository, _unitOfWork,
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<WebhookIngestionService>.Instance);

    [Fact]
    public async Task IngestAsync_PersistsSingleSupportedEvent()
    {
        var service = CreateService();
        var body = """[{"eventId":1,"subscriptionType":"contact.propertyChange","objectId":10,"propertyName":"email","occurredAt":1700000000000,"attemptNumber":0}]""";

        var result = await service.IngestAsync(body, "corr-1");

        Assert.False(result.IsMalformed);
        Assert.Equal(1, result.Accepted);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal(0, result.Ignored);
        var stored = Assert.Single(_repository.Events);
        Assert.Equal(IntegrationEventStatus.Received, stored.Status);
        Assert.Equal(EntityType.Contact, stored.EntityType);
        Assert.Equal("10", stored.EntityId);
    }

    [Fact]
    public async Task IngestAsync_PersistsAllEventsInABatch()
    {
        var service = CreateService();
        var body = """
        [
          {"eventId":1,"subscriptionType":"contact.creation","objectId":10,"occurredAt":1700000000000,"attemptNumber":0},
          {"eventId":2,"subscriptionType":"company.propertyChange","objectId":20,"occurredAt":1700000000001,"attemptNumber":0},
          {"eventId":3,"subscriptionType":"deal.creation","objectId":30,"occurredAt":1700000000002,"attemptNumber":0}
        ]
        """;

        var result = await service.IngestAsync(body, "corr-1");

        Assert.Equal(3, result.Accepted);
        Assert.Equal(3, _repository.Events.Count);
    }

    [Fact]
    public async Task IngestAsync_DetectsDuplicateWithinSameBatch()
    {
        var service = CreateService();
        var body = """
        [
          {"eventId":1,"subscriptionType":"contact.creation","objectId":10,"occurredAt":1700000000000,"attemptNumber":0},
          {"eventId":1,"subscriptionType":"contact.creation","objectId":10,"occurredAt":1700000000000,"attemptNumber":1}
        ]
        """;

        var result = await service.IngestAsync(body, "corr-1");

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.Duplicates);
        Assert.Single(_repository.Events);
    }

    [Fact]
    public async Task IngestAsync_DetectsPreviouslyPersistedDuplicate_AcrossSeparateRequests()
    {
        var service = CreateService();
        var body = """[{"eventId":1,"subscriptionType":"contact.creation","objectId":10,"occurredAt":1700000000000,"attemptNumber":0}]""";

        var first = await service.IngestAsync(body, "corr-1");
        var second = await service.IngestAsync(body, "corr-2");

        Assert.Equal(1, first.Accepted);
        Assert.Equal(0, second.Accepted);
        Assert.Equal(1, second.Duplicates);
        Assert.Single(_repository.Events);
    }

    [Fact]
    public async Task IngestAsync_MarksUnsupportedEventAsIgnored_WithoutBlockingSupportedOnesInSameBatch()
    {
        var service = CreateService();
        var body = """
        [
          {"eventId":1,"subscriptionType":"contact.deletion","objectId":10,"occurredAt":1700000000000,"attemptNumber":0},
          {"eventId":2,"subscriptionType":"contact.propertyChange","objectId":10,"occurredAt":1700000000000,"attemptNumber":0}
        ]
        """;

        var result = await service.IngestAsync(body, "corr-1");

        Assert.Equal(1, result.Accepted);
        Assert.Equal(1, result.Ignored);
        Assert.Contains(_repository.Events, e => e.ExternalEventId == "1" && e.Status == IntegrationEventStatus.Ignored);
        Assert.Contains(_repository.Events, e => e.ExternalEventId == "2" && e.Status == IntegrationEventStatus.Received);
    }

    [Fact]
    public async Task IngestAsync_ReturnsMalformed_ForInvalidJson_AndPersistsNothing()
    {
        var service = CreateService();

        var result = await service.IngestAsync("not json", "corr-1");

        Assert.True(result.IsMalformed);
        Assert.Empty(_repository.Events);
    }
}
