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
    public void IntegrationEvent_DefaultsToPendingStatusWithZeroRetries()
    {
        var integrationEvent = new IntegrationEvent();

        Assert.Equal(IntegrationEventStatus.Pending, integrationEvent.Status);
        Assert.Equal(0, integrationEvent.RetryCount);
    }
}
