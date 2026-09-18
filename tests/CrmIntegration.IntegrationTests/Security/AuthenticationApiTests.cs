using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Security;
using CrmIntegration.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Security;

/// <summary>Real HTTP login -> refresh -> logout flow against the real ASP.NET Core pipeline and PostgreSQL. See docs/SECURITY.md.</summary>
public class AuthenticationApiTests
{
    private static async Task<(Guid UserId, string Email, string Password)> CreateTestUserAsync(CrmApiFactory factory, UserRole role = UserRole.Sales, bool isActive = true)
    {
        using var scope = factory.Services.CreateScope();
        var userManagement = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var email = $"auth-test.{Guid.NewGuid():N}@example.test";
        const string password = "CorrectHorse123";
        var created = await userManagement.CreateAsync(email, password, role);
        if (!isActive)
        {
            await userManagement.SetActiveStatusAsync(created.Id, false);
        }
        return (created.Id, email, password);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAccessAndRefreshTokens()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (userId, email, password) = await CreateTestUserAsync(factory);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(userId, body!.UserId);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.True(body.AccessTokenExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (_, email, _) = await CreateTestUserAsync(factory);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "WrongPassword456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized_IndistinguishableFromWrongPassword()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (_, email, _) = await CreateTestUserAsync(factory);

        var wrongPasswordResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "WrongPassword456" });
        var unknownEmailResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "nobody-here@example.test", Password = "WrongPassword456" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmailResponse.StatusCode);
        // Compare everything except the per-request correlationId (a TraceIdentifier, expected to differ every call).
        var problem1 = await wrongPasswordResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var problem2 = await unknownEmailResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(problem1.GetProperty("title").GetString(), problem2.GetProperty("title").GetString());
        Assert.Equal(problem1.GetProperty("status").GetInt32(), problem2.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Login_InactiveUser_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (_, email, password) = await CreateTestUserAsync(factory, isActive: false);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUserIdentity()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (userId, email, password) = await CreateTestUserAsync(factory, UserRole.Operations);
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password }))
            .Content.ReadFromJsonAsync<LoginResponse>(CrmApiFactory.JsonOptions);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(userId, me!.Id);
        Assert.Equal(email, me.Email);
        Assert.Equal(UserRole.Operations, me.Role);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ValidToken_RotatesAndReturnsNewTokens()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (_, email, password) = await CreateTestUserAsync(factory);
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password }))
            .Content.ReadFromJsonAsync<LoginResponse>(CrmApiFactory.JsonOptions);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = login!.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<RefreshResponse>(CrmApiFactory.JsonOptions);
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);
        Assert.NotEqual(login.AccessToken, refreshed.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReplayOfAnAlreadyRotatedToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (_, email, password) = await CreateTestUserAsync(factory);
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password }))
            .Content.ReadFromJsonAsync<LoginResponse>(CrmApiFactory.JsonOptions);

        await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = login!.RefreshToken });
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Refresh_UnknownToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = "not-a-real-refresh-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken_SoASubsequentRefreshFails()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (_, email, password) = await CreateTestUserAsync(factory);
        var login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password }))
            .Content.ReadFromJsonAsync<LoginResponse>(CrmApiFactory.JsonOptions);

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);
        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var refreshAfterLogout = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = login.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAnAccessToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = "irrelevant" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithAnotherUsersRefreshToken_ReturnsUnauthorized()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var (_, emailA, passwordA) = await CreateTestUserAsync(factory);
        var (_, emailB, passwordB) = await CreateTestUserAsync(factory);
        var loginA = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = emailA, Password = passwordA }))
            .Content.ReadFromJsonAsync<LoginResponse>(CrmApiFactory.JsonOptions);
        var loginB = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = emailB, Password = passwordB }))
            .Content.ReadFromJsonAsync<LoginResponse>(CrmApiFactory.JsonOptions);

        // User B is authenticated but tries to log out User A's refresh token.
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginB!.AccessToken);
        var response = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = loginA!.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
