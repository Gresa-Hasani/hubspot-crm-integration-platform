using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class LifecycleReportTests
{
    private static Contact NewContact(LifecycleStage stage) => new()
    {
        Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = Guid.NewGuid() + "@x.com",
        LifecycleStage = stage, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetLifecycleAsync_EmptyDatabase_ReturnsEmptyDistribution()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetLifecycleAsync();

        Assert.Equal(0, result.TotalContacts);
        Assert.Empty(result.StageDistribution);
    }

    [Fact]
    public async Task GetLifecycleAsync_CountsByStage_AndComputesPercentageDistribution()
    {
        var repo = new FakeSalesReportingRepository();
        repo.Contacts.Add(NewContact(LifecycleStage.Lead));
        repo.Contacts.Add(NewContact(LifecycleStage.Lead));
        repo.Contacts.Add(NewContact(LifecycleStage.Customer));
        var service = new SalesReportingService(repo);

        var result = await service.GetLifecycleAsync();

        Assert.Equal(3, result.TotalContacts);
        var lead = result.StageDistribution.Single(s => s.Stage == LifecycleStage.Lead);
        var customer = result.StageDistribution.Single(s => s.Stage == LifecycleStage.Customer);
        Assert.Equal(2, lead.ContactCount);
        Assert.Equal(66.67m, lead.PercentageOfTotal);
        Assert.Equal(1, customer.ContactCount);
        Assert.Equal(33.33m, customer.PercentageOfTotal);
    }

    [Fact]
    public async Task GetLifecycleAsync_AggregatesObservedTransitions()
    {
        var repo = new FakeSalesReportingRepository();
        var contact = NewContact(LifecycleStage.Customer);
        repo.Contacts.Add(contact);
        repo.ContactLifecycleTransitions.Add(new ContactLifecycleTransition
        {
            Id = Guid.NewGuid(), ContactId = contact.Id, FromStage = LifecycleStage.Lead, ToStage = LifecycleStage.Customer,
            OccurredAt = DateTime.UtcNow, Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = DateTime.UtcNow
        });
        var service = new SalesReportingService(repo);

        var result = await service.GetLifecycleAsync();

        var transition = Assert.Single(result.ObservedTransitions);
        Assert.Equal(LifecycleStage.Lead, transition.FromStage);
        Assert.Equal(LifecycleStage.Customer, transition.ToStage);
        Assert.Equal(1, transition.TransitionCount);

        var recent = Assert.Single(result.RecentChanges);
        Assert.Equal(contact.Id, recent.ContactId);
    }
}
