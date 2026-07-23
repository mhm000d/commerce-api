using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Contracts.Auth;
using Commerce.Contracts.Addresses;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

[Collection("Database")]
public sealed class AddressesApiTests(ApiFactory factory)
    : IAsyncLifetime
{
    private HttpClient _client = null!;

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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(string accessToken, Guid userId)> RegisterAsync(
        string email = "user@example.com",
        string name = "Test User")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(name, email, "Password1", Phone: null));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (body!.AccessToken, body.User.Id);
    }

    private void Authorize(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private static AddressRequest ValidRequest(
        bool isDefault = false, string fullName = "John Doe") =>
        new(
            FullName: fullName,
            PhoneNumber: "+201012345678",
            Country: "Egypt",
            Governorate: "Cairo",
            Area: "Nasr City",
            Street: "Abbas El Akkad",
            BuildingNumber: null,
            Floor: null,
            Apartment: null,
            AddressName: "Home",
            IsDefault: isDefault
        );

    // Creates an address through the API and returns the parsed response body.
    private async Task<AddressResponse> CreateAddressAsync(bool isDefault = false)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/addresses", ValidRequest(isDefault));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AddressResponse>())!;
    }

    // ── GET /api/addresses ────────────────────────────────────────────────────

    [Fact]
    public async Task Get_WhenAuthenticated_Returns200WithAddressList()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);

        await CreateAddressAsync();

        var response = await _client.GetAsync("/api/v1/addresses");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<AddressResponse>>();
        body.ShouldNotBeNull();
        body.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Get_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/addresses");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ShouldNotReturnOtherUsersAddresses()
    {
        // Register two users and give each one an address.
        var (tokenA, _) = await RegisterAsync("a@example.com", "User A");
        Authorize(tokenA);
        await CreateAddressAsync();

        var (tokenB, _) = await RegisterAsync("b@example.com", "User B");
        Authorize(tokenB);
        await CreateAddressAsync();

        // Fetch user B's list — should only see B's one address.
        var response = await _client.GetAsync("/api/v1/addresses");
        var body = await response.Content.ReadFromJsonAsync<List<AddressResponse>>();

        body!.Count.ShouldBe(1);
        body[0].FullName.ShouldBe("John Doe"); // not user A's address
    }

    // ── POST /api/addresses ───────────────────────────────────────────────────

    [Fact]
    public async Task Post_WithValidData_Returns201AndAddressResponse()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/addresses", ValidRequest(fullName: "Jane Doe"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body.ShouldNotBeNull();
        body.FullName.ShouldBe("Jane Doe");
        body.IsDefault.ShouldBeTrue(); // first address is always default
    }

    [Fact]
    public async Task Post_WhenUnauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/addresses", ValidRequest());
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WithInvalidData_Returns400()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);

        // Empty FullName should fail FluentValidation.
        var bad = ValidRequest() with { FullName = "" };
        var response = await _client.PostAsJsonAsync("/api/v1/addresses", bad);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_SecondAddress_WithIsDefaultTrue_ShouldDemotePreviousDefault()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);

        var first = await CreateAddressAsync(); // auto-default
        var second = await CreateAddressAsync(isDefault: true); // new default

        first.IsDefault.ShouldBeTrue(); // was true at creation time
        second.IsDefault.ShouldBeTrue(); // new one is also true in its response

        // Fetch the list to see the final DB state.
        var listResponse = await _client.GetAsync("/api/v1/addresses");
        var all = await listResponse.Content.ReadFromJsonAsync<List<AddressResponse>>();

        all!.Count(a => a.IsDefault).ShouldBe(1);
        all!.Single(a => a.IsDefault).Id.ShouldBe(second.Id);
    }

    // ── PUT /api/addresses/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Put_ByOwner_Returns200WithUpdatedData()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var address = await CreateAddressAsync();

        // Ensure we keep the address as default (the only one)
        var updated = ValidRequest(fullName: "Updated Name") with { IsDefault = true };
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/addresses/{address.Id}", updated);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AddressResponse>();
        body!.FullName.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task Put_WhenUnauthenticated_Returns401()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var address = await CreateAddressAsync();

        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/addresses/{address.Id}", ValidRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_WhenNotOwner_Returns403()
    {
        var (ownerToken, _) = await RegisterAsync("owner@example.com", "Owner");
        Authorize(ownerToken);
        var address = await CreateAddressAsync();

        // Intruder tries to update the owner's address.
        var (intruderToken, _) = await RegisterAsync("intruder@example.com", "Intruder");
        Authorize(intruderToken);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/addresses/{address.Id}", ValidRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_WhenNotFound_Returns404()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/addresses/{Guid.NewGuid()}", ValidRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_WithInvalidData_Returns400()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var address = await CreateAddressAsync();

        var bad = ValidRequest() with { PhoneNumber = "abc" }; // fails regex
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/addresses/{address.Id}", bad);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/addresses/{id} ────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByOwner_Returns204()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var address = await CreateAddressAsync();

        var response = await _client.DeleteAsync($"/api/v1/addresses/{address.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WhenUnauthenticated_Returns401()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var address = await CreateAddressAsync();

        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.DeleteAsync($"/api/v1/addresses/{address.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WhenNotOwner_Returns403()
    {
        var (ownerToken, _) = await RegisterAsync("owner@example.com", "Owner");
        Authorize(ownerToken);
        var address = await CreateAddressAsync();

        var (intruderToken, _) = await RegisterAsync("intruder@example.com", "Intruder");
        Authorize(intruderToken);

        var response = await _client.DeleteAsync($"/api/v1/addresses/{address.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_WhenNotFound_Returns404()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);

        var response = await _client.DeleteAsync($"/api/v1/addresses/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_DefaultAddress_ShouldPromoteRemainingAddressViaApi()
    {
        // This test verifies Decision C survives the full HTTP stack,
        // not just the service layer in isolation.
        var (token, _) = await RegisterAsync();
        Authorize(token);

        var defaultOne = await CreateAddressAsync(); // auto-default
        var other = await CreateAddressAsync(isDefault: false);

        await _client.DeleteAsync($"/api/v1/addresses/{defaultOne.Id}");

        var listResponse = await _client.GetAsync("/api/v1/addresses");
        var all = await listResponse.Content.ReadFromJsonAsync<List<AddressResponse>>();

        all!.Count.ShouldBe(1);
        all[0].Id.ShouldBe(other.Id);
        all[0].IsDefault.ShouldBeTrue(); // promoted
    }
}