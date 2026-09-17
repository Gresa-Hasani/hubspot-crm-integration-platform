using System.Text.Json;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Xunit;

namespace CrmIntegration.UnitTests.Domain;

public class DomainDefaultsTests
{
    [Fact]
    public void Deal_DefaultsToOpenStatusAndQualifiedToBuyStage()
    {
        var deal = new Deal();

        Assert.Equal(DealStatus.Open, deal.Status);
        Assert.Equal(DealStage.QualifiedToBuy, deal.Stage);
    }

    [Fact]
    public void Contact_DefaultsToLeadLifecycleStage()
    {
        var contact = new Contact();

        Assert.Equal(LifecycleStage.Lead, contact.LifecycleStage);
    }

    [Fact]
    public void OnboardingRecord_DefaultsToPendingStatus()
    {
        var onboarding = new OnboardingRecord();

        Assert.Equal(OnboardingStatus.Pending, onboarding.Status);
    }

    [Fact]
    public void IntegrationEvent_DefaultsToReceivedStatusWithZeroAttempts()
    {
        var integrationEvent = new IntegrationEvent();

        Assert.Equal(IntegrationEventStatus.Received, integrationEvent.Status);
        Assert.Equal(0, integrationEvent.AttemptCount);
    }

    [Fact]
    public void IntegrationEvent_DefaultPayload_IsValidJson()
    {
        // Regression test: Payload maps to a jsonb column. An empty-string default (rather than
        // "{}") fails at the database with "invalid input syntax for type json" the moment any
        // code constructs an IntegrationEvent without explicitly setting Payload — discovered via
        // a failing integration test during Phase 5.
        var integrationEvent = new IntegrationEvent();

        var exception = Record.Exception(() => JsonDocument.Parse(integrationEvent.Payload));

        Assert.Null(exception);
    }
}
