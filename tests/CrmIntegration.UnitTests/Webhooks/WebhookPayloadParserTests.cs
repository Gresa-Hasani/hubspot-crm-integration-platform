using CrmIntegration.Infrastructure.Webhooks;
using Xunit;

namespace CrmIntegration.UnitTests.Webhooks;

public class WebhookPayloadParserTests
{
    private readonly WebhookPayloadParser _parser = new();

    [Fact]
    public void TryParse_ParsesSingleEvent()
    {
        var body = """[{"eventId":123,"subscriptionType":"contact.propertyChange","objectId":456,"propertyName":"email","occurredAt":1700000000000,"attemptNumber":0}]""";

        var success = _parser.TryParse(body, out var events, out var error);

        Assert.True(success);
        Assert.Null(error);
        var single = Assert.Single(events);
        Assert.Equal("123", single.EventId);
        Assert.Equal("contact.propertyChange", single.SubscriptionType);
        Assert.Equal("456", single.ObjectId);
        Assert.Equal("email", single.PropertyName);
        Assert.Equal(0, single.AttemptNumber);
    }

    [Fact]
    public void TryParse_ParsesMultipleEventsInOneBatch()
    {
        var body = """
        [
          {"eventId":1,"subscriptionType":"contact.creation","objectId":10,"occurredAt":1700000000000,"attemptNumber":0},
          {"eventId":2,"subscriptionType":"company.propertyChange","objectId":20,"occurredAt":1700000000001,"attemptNumber":0}
        ]
        """;

        var success = _parser.TryParse(body, out var events, out var error);

        Assert.True(success);
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForMalformedJson()
    {
        var success = _parser.TryParse("not json at all", out var events, out var error);

        Assert.False(success);
        Assert.Empty(events);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForMissingRequiredField()
    {
        var body = """[{"objectId":456,"occurredAt":1700000000000}]"""; // missing eventId/subscriptionType

        var success = _parser.TryParse(body, out var events, out var error);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_IgnoresUnknownAdditionalFields()
    {
        var body = """[{"eventId":1,"subscriptionType":"contact.creation","objectId":10,"occurredAt":1700000000000,"attemptNumber":0,"someFutureField":"abc","portalId":999}]""";

        var success = _parser.TryParse(body, out var events, out var error);

        Assert.True(success);
        Assert.Single(events);
    }

    [Fact]
    public void TryParse_ReturnsFalse_ForEmptyArray()
    {
        var success = _parser.TryParse("[]", out var events, out var error);

        Assert.True(success);
        Assert.Empty(events);
    }
}
