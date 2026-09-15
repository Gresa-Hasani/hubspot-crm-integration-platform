using CrmIntegration.Application.Configuration;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync.Mapping;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Sync.Mapping;

public class ContactHubSpotMapperTests
{
    private static ContactHubSpotMapper CreateMapper() =>
        new(new HubSpotStageMapper(Options.Create(new HubSpotOptions())));

    [Fact]
    public void ToHubSpotProperties_MapsAllFields()
    {
        var mapper = CreateMapper();
        var contact = new Contact
        {
            Email = "alice@acme.example",
            FirstName = "Alice",
            LastName = "Smith",
            Phone = "+15551234567",
            JobTitle = "Engineer",
            LifecycleStage = LifecycleStage.SalesQualifiedLead
        };

        var properties = mapper.ToHubSpotProperties(contact);

        Assert.Equal("alice@acme.example", properties["email"]);
        Assert.Equal("Alice", properties["firstname"]);
        Assert.Equal("Smith", properties["lastname"]);
        Assert.Equal("+15551234567", properties["phone"]);
        Assert.Equal("Engineer", properties["jobtitle"]);
        Assert.Equal("salesqualifiedlead", properties["lifecyclestage"]);
    }

    [Fact]
    public void ApplyHubSpotProperties_NormalizesIncomingValues()
    {
        var mapper = CreateMapper();
        var contact = new Contact();
        var record = new HubSpotRecord("1", new Dictionary<string, string?>
        {
            ["email"] = "  Bob@ACME.Example  ",
            ["firstname"] = "Bob",
            ["lastname"] = "Jones",
            ["phone"] = "+1 (555) 999-8888",
            ["lifecyclestage"] = "customer"
        }, null, null);

        mapper.ApplyHubSpotProperties(contact, record);

        Assert.Equal("bob@acme.example", contact.Email);
        Assert.Equal("+15559998888", contact.Phone);
        Assert.Equal(LifecycleStage.Customer, contact.LifecycleStage);
    }

    [Fact]
    public void ApplyHubSpotProperties_IgnoresUnknownLifecycleStage()
    {
        var mapper = CreateMapper();
        var contact = new Contact { LifecycleStage = LifecycleStage.Lead };
        var record = new HubSpotRecord("1", new Dictionary<string, string?>
        {
            ["lifecyclestage"] = "some_future_hubspot_value_we_dont_know"
        }, null, null);

        mapper.ApplyHubSpotProperties(contact, record);

        Assert.Equal(LifecycleStage.Lead, contact.LifecycleStage);
    }
}
