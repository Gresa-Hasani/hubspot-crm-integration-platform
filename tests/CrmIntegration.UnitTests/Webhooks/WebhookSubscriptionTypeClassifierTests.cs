using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Enums;
using Xunit;

namespace CrmIntegration.UnitTests.Webhooks;

public class WebhookSubscriptionTypeClassifierTests
{
    [Theory]
    [InlineData("contact.propertyChange", EntityType.Contact)]
    [InlineData("contact.creation", EntityType.Contact)]
    [InlineData("company.propertyChange", EntityType.Company)]
    [InlineData("company.creation", EntityType.Company)]
    [InlineData("deal.propertyChange", EntityType.Deal)]
    [InlineData("deal.creation", EntityType.Deal)]
    public void IsSupported_ReturnsTrue_ForCreationAndPropertyChangeOnTrackedObjects(string subscriptionType, EntityType expectedType)
    {
        Assert.True(WebhookSubscriptionTypeClassifier.IsSupported(subscriptionType));
        Assert.Equal(expectedType, WebhookSubscriptionTypeClassifier.TryGetEntityType(subscriptionType));
    }

    [Theory]
    [InlineData("contact.deletion")]
    [InlineData("contact.privacyDeletion")]
    [InlineData("company.deletion")]
    [InlineData("deal.deletion")]
    public void IsSupported_ReturnsFalse_ForDeletionEvents(string subscriptionType)
    {
        Assert.False(WebhookSubscriptionTypeClassifier.IsSupported(subscriptionType));
    }

    [Theory]
    [InlineData("ticket.propertyChange")]
    [InlineData("ticket.creation")]
    [InlineData("conversation.newMessage")]
    [InlineData("unknown-format")]
    public void IsSupported_ReturnsFalse_ForUntrackedObjectTypes(string subscriptionType)
    {
        Assert.False(WebhookSubscriptionTypeClassifier.IsSupported(subscriptionType));
    }

    [Fact]
    public void TryGetEntityType_ReturnsNull_ForUntrackedObjectType()
    {
        Assert.Null(WebhookSubscriptionTypeClassifier.TryGetEntityType("ticket.propertyChange"));
    }
}
