using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Contracts.Auth;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

[Collection("Database")]
public sealed class AuthApiTests(ApiFactory factory)
    : IAsyncLifetime
{
    private HttpClient _client = null!;

    // Reset DB + create a fresh client before every test so
    // DefaultRequestHeaders never bleed between tests.
    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _client = factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Register_Returns201_AndTokens()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Adam Hassan", "adam@example.com", "Password1", Phone: null));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        body.User.Email.ShouldBe("adam@example.com");
    }

    [Fact]
    public async Task Login_Returns200_AndTokens()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Adam Hassan", "adam@example.com", "Password1", Phone: null));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("adam@example.com", "Password1"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LogoutAll_RequiresBearerToken_Returns204()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Adam Hassan", "adam@example.com", "Password1", Phone: null));

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        registerBody.ShouldNotBeNull();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", registerBody.AccessToken);

        var response = await _client.PostAsync("/api/v1/auth/logout-all", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}