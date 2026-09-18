using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Security;

/// <summary>Admin-only user management, through real HTTP against real PostgreSQL. See docs/SECURITY.md "User management" and "Last-Admin safety".</summary>
public class UserManagementApiTests
{
    [Fact]
    public async Task Create_ValidRequest_ReturnsCreated_AndNeverIncludesPasswordHash()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var email = $"new-user.{Guid.NewGuid():N}@example.test";

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = email, Password = "CorrectHorse123", Role = UserRole.Sales });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        var created = await response.Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions);
        Assert.Equal(email, created!.Email);
        Assert.Equal(UserRole.Sales, created.Role);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsConflict()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var email = $"dup.{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = email, Password = "CorrectHorse123", Role = UserRole.Sales });

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = email.ToUpperInvariant(), Password = "AnotherPass456", Role = UserRole.Operations });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_WeakPassword_ReturnsBadRequest()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = $"weak.{Guid.NewGuid():N}@example.test", Password = "weak", Role = UserRole.Sales });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidRoleValue_ReturnsBadRequest_ViaFrameworkModelBinding()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await client.PostAsync("/api/users", new StringContent(
            $$"""{"email":"invalid-role.{{Guid.NewGuid():N}}@example.test","password":"CorrectHorse123","role":"SuperAdmin"}""",
            System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_And_GetById_ReturnCreatedUsers()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var email = $"lookup.{Guid.NewGuid():N}@example.test";
        var created = await (await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = email, Password = "CorrectHorse123", Role = UserRole.ReadOnly }))
            .Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions);

        var getResponse = await client.GetAsync($"/api/users/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/users");
        var list = await listResponse.Content.ReadFromJsonAsync<List<UserSummary>>(CrmApiFactory.JsonOptions);
        Assert.Contains(list!, u => u.Id == created.Id);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNotFound()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeRole_OnANonAdminUser_Succeeds()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var created = await (await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = $"role-change.{Guid.NewGuid():N}@example.test", Password = "CorrectHorse123", Role = UserRole.Sales }))
            .Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions);

        var response = await client.PutAsJsonAsync($"/api/users/{created!.Id}/role", new ChangeUserRoleRequest { Role = UserRole.Operations });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions);
        Assert.Equal(UserRole.Operations, updated!.Role);
    }

    [Fact]
    public async Task SetStatus_DeactivateThenReactivate_ANonAdminUser_Succeeds()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var created = await (await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = $"status-change.{Guid.NewGuid():N}@example.test", Password = "CorrectHorse123", Role = UserRole.Sales }))
            .Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions);

        var deactivateResponse = await client.PutAsJsonAsync($"/api/users/{created!.Id}/status", new SetUserStatusRequest { IsActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.False((await deactivateResponse.Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions))!.IsActive);

        var reactivateResponse = await client.PutAsJsonAsync($"/api/users/{created.Id}/status", new SetUserStatusRequest { IsActive = true });
        Assert.True((await reactivateResponse.Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions))!.IsActive);
    }

    [Fact]
    public async Task ResetPassword_ValidPassword_AllowsLoginWithTheNewPassword()
    {
        using var factory = new CrmApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var email = $"reset.{Guid.NewGuid():N}@example.test";
        var created = await (await client.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = email, Password = "OriginalPass123", Role = UserRole.Sales }))
            .Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions);

        var resetResponse = await client.PostAsJsonAsync($"/api/users/{created!.Id}/reset-password", new ResetPasswordRequest { NewPassword = "BrandNewPass456" });
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);

        var loginWithOld = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "OriginalPass123" });
        var loginWithNew = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "BrandNewPass456" });

        Assert.Equal(HttpStatusCode.Unauthorized, loginWithOld.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginWithNew.StatusCode);
    }

    [Fact]
    public async Task LastAdminProtection_CannotDeactivateOrDemote_TheSoleActiveAdmin_ThroughRealHttpAndPostgres()
    {
        using var factory = new CrmApiFactory();
        // Obtain a token bound to the fixture Admin BEFORE touching anyone's active status — JWTs
        // are stateless and not re-checked against IsActive per request (docs/SECURITY.md "Logout
        // /revocation semantics"), so this token keeps working for the rest of this test even
        // after we deactivate its own account below.
        var fixtureAdmin = await factory.EnsureTestUserAsync(UserRole.Admin);
        var adminClient = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManagement = scope.ServiceProvider.GetRequiredService<IUserManagementService>();

        var otherActiveAdminIds = await db.ApplicationUsers
            .Where(u => u.Role == UserRole.Admin && u.IsActive && u.Id != fixtureAdmin.Id)
            .Select(u => u.Id)
            .ToListAsync();

        try
        {
            // Make the fixture Admin the ONLY active Admin in the whole (shared) database.
            foreach (var id in otherActiveAdminIds)
            {
                await userManagement.SetActiveStatusAsync(id, false);
            }

            var deactivateResponse = await adminClient.PutAsJsonAsync($"/api/users/{fixtureAdmin.Id}/status", new SetUserStatusRequest { IsActive = false });
            Assert.Equal(HttpStatusCode.Conflict, deactivateResponse.StatusCode);

            var demoteResponse = await adminClient.PutAsJsonAsync($"/api/users/{fixtureAdmin.Id}/role", new ChangeUserRoleRequest { Role = UserRole.Operations });
            Assert.Equal(HttpStatusCode.Conflict, demoteResponse.StatusCode);

            // Confirm neither request actually took effect.
            var reloaded = await db.ApplicationUsers.AsNoTracking().SingleAsync(u => u.Id == fixtureAdmin.Id);
            Assert.True(reloaded.IsActive);
            Assert.Equal(UserRole.Admin, reloaded.Role);
        }
        finally
        {
            // Restore every admin we touched so no other test in the shared database is affected.
            foreach (var id in otherActiveAdminIds)
            {
                await userManagement.SetActiveStatusAsync(id, true);
            }
        }
    }

    [Fact]
    public async Task LastAdminProtection_DoesNotBlock_DemotingOneOfSeveralActiveAdmins()
    {
        using var factory = new CrmApiFactory();
        var adminClient = await factory.CreateAuthenticatedClientAsync(UserRole.Admin);
        var extraAdmin = await (await adminClient.PostAsJsonAsync("/api/users", new CreateUserRequest { Email = $"extra-admin.{Guid.NewGuid():N}@example.test", Password = "CorrectHorse123", Role = UserRole.Admin }))
            .Content.ReadFromJsonAsync<UserSummary>(CrmApiFactory.JsonOptions);

        // At least 2 active Admins now exist (the fixture Admin + this new one) — demoting the new one must succeed.
        var response = await adminClient.PutAsJsonAsync($"/api/users/{extraAdmin!.Id}/status", new SetUserStatusRequest { IsActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
