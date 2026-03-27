using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Auth;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

public sealed class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_Returns201_AndTokens()
    {
        var request = new RegisterRequest(
            Name: "Adam Hassan",
            Email: "adam@example.com", /*"ahmed_register@example.com",*/
            Password: "Password1",
            Phone: null
        );

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

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
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "Adam Hassan", "adam@example.com", "Password1", Phone: null));

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "adam@example.com", "Password1"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LogoutAll_RequiresBearerToken()
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            "Adam Hassan", "adm@example.com", "Password1", Phone: null));

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        registerBody.ShouldNotBeNull();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", registerBody.AccessToken);

        var response = await _client.PostAsync("/api/auth/logout-all", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}