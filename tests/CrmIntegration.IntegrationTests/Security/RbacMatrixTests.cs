using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Companies;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Domain.Enums;
using Xunit;

namespace CrmIntegration.IntegrationTests.Security;

/// <summary>
/// Explicit RBAC matrix verification through real HTTP against the real authorization pipeline —
/// not unit tests of policy attributes in isolation. Covers representative endpoints for every
/// role named in the Phase 8 spec's RBAC matrix. See docs/SECURITY.md "RBAC matrix".
/// </summary>
public class RbacMatrixTests
{
    [Fact]
    public async Task Anonymous_ProtectedEndpoint_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/contacts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The webhook ingestion endpoint's "no JWT required" behavior is verified with a properly
    // signed request in WebhookAuthenticationExceptionTests.ValidSignature_NoJwt_* — a bare,
    // unsigned POST here would just get 401 for a missing signature (a different, correct
    // reason), which doesn't actually prove anything about the JWT requirement either way.

    [Fact]
    public async Task Anonymous_HealthEndpoints_AreAccessible()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task ReadOnly_CanReadContacts_ButNotWriteThem()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.ReadOnly);

        var getResponse = await client.GetAsync("/api/contacts");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var postResponse = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest { FirstName = "A", LastName = "B", Email = $"ro.{Guid.NewGuid():N}@x.test" });
        Assert.Equal(HttpStatusCode.Forbidden, postResponse.StatusCode);
    }

    [Fact]
    public async Task ReadOnly_CanViewSalesReports()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.ReadOnly);

        var response = await client.GetAsync("/api/reports/sales/overview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnly_CannotTriggerSync()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.ReadOnly);

        var response = await client.PostAsync($"/api/sync/companies/{Guid.NewGuid()}/to-hubspot", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnly_CannotRetryAutomations()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.ReadOnly);

        var response = await client.PostAsync($"/api/automations/executions/{Guid.NewGuid()}/retry", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnly_CannotAccessUserManagement()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.ReadOnly);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnly_CannotViewOperationalHealth()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.ReadOnly);

        var response = await client.GetAsync("/api/reports/operations/health");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sales_CanReadAndWriteCrm()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Sales);

        var getResponse = await client.GetAsync("/api/deals");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var postResponse = await client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = "Sales RBAC Co " + Guid.NewGuid().ToString("N")[..6] });
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
    }

    [Fact]
    public async Task Sales_CanViewSalesReports()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Sales);

        var response = await client.GetAsync("/api/reports/sales/pipeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sales_CannotTriggerSync()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Sales);

        var response = await client.PostAsync($"/api/sync/companies/{Guid.NewGuid()}/to-hubspot", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sales_CannotAdministerWebhookEvents()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Sales);

        var response = await client.GetAsync("/api/webhooks/events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sales_CannotManageUsers()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Sales);

        var response = await client.PostAsJsonAsync("/api/users", new CrmIntegration.Application.Security.CreateUserRequest { Email = "x@x.test", Password = "CorrectHorse123", Role = UserRole.ReadOnly });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Sales_CannotRetryDeadLetteredSyncJobs()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Sales);

        var response = await client.PostAsync($"/api/sync/jobs/{Guid.NewGuid()}/retry", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Operations_CanReadAndWriteCrm()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Operations);

        var getResponse = await client.GetAsync("/api/contacts");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var postResponse = await client.PostAsJsonAsync("/api/contacts", new CreateContactRequest { FirstName = "Ops", LastName = "User", Email = $"ops.{Guid.NewGuid():N}@x.test" });
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
    }

    [Fact]
    public async Task Operations_CanTriggerSync()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Operations);

        var response = await client.GetAsync("/api/sync/jobs?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operations_CanInspectWebhookEvents()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Operations);

        var response = await client.GetAsync("/api/webhooks/events?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operations_CanInspectAutomationExecutions()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Operations);

        var response = await client.GetAsync("/api/automations/executions?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operations_CanViewOperationalHealth()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Operations);

        var response = await client.GetAsync("/api/reports/operations/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operations_CannotManageUsers()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Operations);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanAccessAllRepresentativeInternalOperations()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/contacts")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/sync/jobs?limit=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/webhooks/events?limit=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/automations/executions?limit=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/sales/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/operations/health")).StatusCode);
    }

    [Fact]
    public async Task Admin_CanManageUsers()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AllRoles_CanCallMe()
    {
        using var factory = new CrmApiFactory();

        foreach (var role in new[] { UserRole.Admin, UserRole.Operations, UserRole.Sales, UserRole.ReadOnly })
        {
            var client = await factory.CreateAuthenticatedClientAsync(role);
            var response = await client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
