using CrmIntegration.Application.Sync.Matching;
using CrmIntegration.Domain.Entities;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;
using MatchType = CrmIntegration.Application.Sync.MatchType;

namespace CrmIntegration.UnitTests.Sync.Matching;

public class ContactMatchServiceTests
{
    [Fact]
    public async Task FindInternalMatchAsync_ReturnsNoMatch_WhenNoContactHasThatEmail()
    {
        var repo = new FakeContactRepository();
        var service = new ContactMatchService(repo, new FakeHubSpotClient());

        var result = await service.FindInternalMatchAsync("nobody@acme.example");

        Assert.Equal(MatchType.NoMatch, result.MatchType);
    }

    [Fact]
    public async Task FindInternalMatchAsync_ReturnsExactMatch_ForSingleContact()
    {
        var repo = new FakeContactRepository();
        var contact = new Contact { Id = Guid.NewGuid(), Email = "alice@acme.example" };
        repo.Contacts.Add(contact);
        var service = new ContactMatchService(repo, new FakeHubSpotClient());

        var result = await service.FindInternalMatchAsync("alice@acme.example");

        Assert.Equal(MatchType.ExactMatch, result.MatchType);
        Assert.Equal(contact.Id, result.InternalId);
    }

    [Fact]
    public async Task FindInternalMatchAsync_ReturnsAmbiguous_ForMultipleContacts()
    {
        var repo = new FakeContactRepository();
        repo.Contacts.Add(new Contact { Id = Guid.NewGuid(), Email = "dup@acme.example" });
        repo.Contacts.Add(new Contact { Id = Guid.NewGuid(), Email = "dup@acme.example" });
        var service = new ContactMatchService(repo, new FakeHubSpotClient());

        var result = await service.FindInternalMatchAsync("dup@acme.example");

        Assert.Equal(MatchType.Ambiguous, result.MatchType);
        Assert.Null(result.InternalId);
    }

    [Fact]
    public async Task FindHubSpotMatchAsync_ReturnsExactMatch_ForSingleHubSpotContact()
    {
        var hubSpot = new FakeHubSpotClient();
        var created = await hubSpot.CreateContactAsync(new Dictionary<string, string?> { ["email"] = "bob@acme.example" });
        var service = new ContactMatchService(new FakeContactRepository(), hubSpot);

        var result = await service.FindHubSpotMatchAsync("bob@acme.example");

        Assert.Equal(MatchType.ExactMatch, result.MatchType);
        Assert.Equal(created.Id, result.HubSpotId);
    }

    [Fact]
    public async Task FindHubSpotMatchAsync_ReturnsNoMatch_WhenNothingFound()
    {
        var service = new ContactMatchService(new FakeContactRepository(), new FakeHubSpotClient());

        var result = await service.FindHubSpotMatchAsync("nobody@acme.example");

        Assert.Equal(MatchType.NoMatch, result.MatchType);
    }
}
