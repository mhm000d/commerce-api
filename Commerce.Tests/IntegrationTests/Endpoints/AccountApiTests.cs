using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Contracts.Account;
using Commerce.Contracts.Auth;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

[Collection("Database")]
public sealed class AccountApiTests(ApiFactory factory)
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

    private async Task<string> RegisterAndAuthorizeAsync(
        string email = "user@example.com",
        string name = "Test User",
        string password = "Password1")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(name, email, password, Phone: null));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var token = body!.AccessToken;

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return token;
    }

    // ── GET /api/account/profile ─────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_WhenAuthenticated_Returns200WithUserProfile()
    {
        await RegisterAndAuthorizeAsync("john@example.com", "John Doe");

        var response = await _client.GetAsync("/api/account/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.ShouldNotBeNull();
        profile.Email.ShouldBe("john@example.com");
        profile.Name.ShouldBe("John Doe");
        profile.Phone.ShouldBeNull();
        profile.Role.ShouldBe("Customer");
        profile.Id.ShouldNotBe(Guid.Empty);
        profile.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task GetProfile_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/account/profile");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/account/profile ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_WithValidData_Returns200WithUpdatedProfile()
    {
        await RegisterAndAuthorizeAsync("jane@example.com", "Jane Doe");

        var request = new UpdateProfileRequest(
            Name: "Jane Smith",
            Phone: "+20123456789"
        );

        var response = await _client.PutAsJsonAsync("/api/account/profile", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<ProfileResponse>();
        profile.ShouldNotBeNull();
        profile.Name.ShouldBe("Jane Smith");
        profile.Phone.ShouldBe("+20123456789");
        profile.Email.ShouldBe("jane@example.com"); // unchanged
    }

    [Fact]
    public async Task UpdateProfile_WhenUnauthenticated_Returns401()
    {
        var request = new UpdateProfileRequest("New Name", null);

        var response = await _client.PutAsJsonAsync("/api/account/profile", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateProfile_WithEmptyName_Returns400()
    {
        await RegisterAndAuthorizeAsync();

        var request = new UpdateProfileRequest("", "+20123456789");

        var response = await _client.PutAsJsonAsync("/api/account/profile", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_WithNameTooLong_Returns400()
    {
        await RegisterAndAuthorizeAsync();

        var longName = new string('a', 201);
        var request = new UpdateProfileRequest(longName, null);

        var response = await _client.PutAsJsonAsync("/api/account/profile", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateProfile_WithPhoneTooLong_Returns400()
    {
        await RegisterAndAuthorizeAsync();

        var longPhone = new string('1', 31);
        var request = new UpdateProfileRequest("Valid Name", longPhone);

        var response = await _client.PutAsJsonAsync("/api/account/profile", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── POST /api/account/change-password ────────────────────────────────────

    [Fact]
    public async Task ChangePassword_WithValidData_Returns204()
    {
        await RegisterAndAuthorizeAsync("user@example.com", "User", "OldPassword1");

        var request = new ChangePasswordRequest(
            CurrentPassword: "OldPassword1",
            NewPassword: "NewPassword123"
        );

        var response = await _client.PostAsJsonAsync("/api/account/change-password", request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify we can log in with the new password
        _client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("user@example.com", "NewPassword123"));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WhenUnauthenticated_Returns401()
    {
        var request = new ChangePasswordRequest("Old", "New");

        var response = await _client.PostAsJsonAsync("/api/account/change-password", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithIncorrectCurrentPassword_Returns401()
    {
        await RegisterAndAuthorizeAsync("user@example.com", "User", "CorrectPassword1");

        var request = new ChangePasswordRequest(
            CurrentPassword: "WrongPassword",
            NewPassword: "NewPassword123"
        );

        var response = await _client.PostAsJsonAsync("/api/account/change-password", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithNewPasswordTooShort_Returns400()
    {
        await RegisterAndAuthorizeAsync("user@example.com", "User", "OldPassword1");

        var request = new ChangePasswordRequest(
            CurrentPassword: "OldPassword1",
            NewPassword: "short"
        );

        var response = await _client.PostAsJsonAsync("/api/account/change-password", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithNewPasswordNoLetter_Returns400()
    {
        await RegisterAndAuthorizeAsync("user@example.com", "User", "OldPassword1");

        var request = new ChangePasswordRequest(
            CurrentPassword: "OldPassword1",
            NewPassword: "12345678"
        );

        var response = await _client.PostAsJsonAsync("/api/account/change-password", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithNewPasswordNoNumber_Returns400()
    {
        await RegisterAndAuthorizeAsync("user@example.com", "User", "OldPassword1");

        var request = new ChangePasswordRequest(
            CurrentPassword: "OldPassword1",
            NewPassword: "ABCDEFGH"
        );

        var response = await _client.PostAsJsonAsync("/api/account/change-password", request);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_RevokesRefreshTokens_ForcingReLogin()
    {
        var email = "user@example.com";
        var password = "OldPassword1";
        var newPassword = "NewPassword123";

        // Register the user
        var regResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("User", email, password, null));
        regResponse.EnsureSuccessStatusCode();
        var regBody = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();

        // Authorize with the access token
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", regBody!.AccessToken);

        // Change password
        var changeRequest = new ChangePasswordRequest(password, newPassword);
        var changeResponse = await _client.PostAsJsonAsync("/api/account/change-password", changeRequest);
        changeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Try to refresh with the old refresh token – should fail (401)
        _client.DefaultRequestHeaders.Authorization = null;
        var refreshPayload = new { refreshToken = regBody.RefreshToken };
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", refreshPayload);
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }}