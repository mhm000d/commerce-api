using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Application.Models;
using Commerce.Contracts.Auth;
using Commerce.Contracts.Ratings;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

public sealed class RatingsApiTests(ApiFactory factory)
    : IClassFixture<ApiFactory>, IAsyncLifetime
{
    // Fresh client per test via InitializeAsync — prevents DefaultRequestHeaders
    // set in one test from bleeding into the next.
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
        string email = "user@example.com", string name = "Test User")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(name, email, "Password1", Phone: null));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (body!.AccessToken, body.User.Id);
    }

    private void Authorize(string accessToken)
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    // Bypasses the admin HTTP layer — products are seed data for rating tests,
    // not the thing under test here.
    private async Task<Product> SeedProductAsync()
    {
        await using var db = factory.CreateDbContext();

        var product = Product.Create(
            name: "Test Product",
            description: "A product for rating tests.",
            price: 29.99m,
            stockQuantity: 100,
            category: Category.Electronics
        );

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private async Task<RatingResponse> CreateRatingAsync(
        Guid productId, int score = 5, string? comment = "Great!")
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/products/{productId}/ratings",
            new RatingRequest(score, comment));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RatingResponse>();
        return body!;
    }

    // ── POST /api/products/{productId}/ratings ────────────────────────────────

    [Fact]
    public async Task Post_WithValidData_Returns201AndRatingResponse()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/products/{product.Id}/ratings",
            new RatingRequest(Score: 4, Comment: "Very good."));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<RatingResponse>();
        body.ShouldNotBeNull();
        body.Score.ShouldBe(4);
        body.Comment.ShouldBe("Very good.");
        body.UserName.ShouldBe("Test User");
    }

    [Fact]
    public async Task Post_WhenUnauthenticated_Returns401()
    {
        var product = await SeedProductAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/products/{product.Id}/ratings",
            new RatingRequest(Score: 4, Comment: null));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WithInvalidScore_Returns400()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/products/{product.Id}/ratings",
            new RatingRequest(Score: 99, Comment: null)); // invalid

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_WhenDuplicateRating_Returns409()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();

        // First rating
        await CreateRatingAsync(product.Id);

        // Second rating — same user, same product
        var response = await _client.PostAsJsonAsync(
            $"/api/products/{product.Id}/ratings",
            new RatingRequest(Score: 3, Comment: null));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── PUT /api/ratings/{id} ─────────────────────────────────────────────────

    [Fact]
    public async Task Put_ByOwner_Returns200WithUpdatedData()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();
        var rating = await CreateRatingAsync(product.Id, score: 5, comment: "Loved it");

        var response = await _client.PutAsJsonAsync(
            $"/api/ratings/{rating.Id}",
            new RatingRequest(Score: 2, Comment: "Changed my mind"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<RatingResponse>();
        body!.Score.ShouldBe(2);
        body.Comment.ShouldBe("Changed my mind");
    }

    [Fact]
    public async Task Put_WhenUnauthenticated_Returns401()
    {
        // Register and create a rating, then remove auth header
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();
        var rating = await CreateRatingAsync(product.Id);

        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.PutAsJsonAsync(
            $"/api/ratings/{rating.Id}",
            new RatingRequest(Score: 1, Comment: null));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_WhenNotOwner_Returns403()
    {
        // Owner creates a rating
        var (ownerToken, _) = await RegisterAsync("owner@example.com", "Owner");
        Authorize(ownerToken);
        var product = await SeedProductAsync();
        var rating = await CreateRatingAsync(product.Id, score: 5);

        // Intruder tries to update it
        var (intruderToken, _) = await RegisterAsync("intruder@example.com", "Intruder");
        Authorize(intruderToken);

        var response = await _client.PutAsJsonAsync(
            $"/api/ratings/{rating.Id}",
            new RatingRequest(Score: 1, Comment: "Sabotage"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_WithInvalidScore_Returns400()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();
        var rating = await CreateRatingAsync(product.Id, score: 5);

        var response = await _client.PutAsJsonAsync(
            $"/api/ratings/{rating.Id}",
            new RatingRequest(Score: 0, Comment: null)); // invalid

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/ratings/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByOwner_Returns204()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();
        var rating = await CreateRatingAsync(product.Id);

        var response = await _client.DeleteAsync($"/api/ratings/{rating.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_WhenUnauthenticated_Returns401()
    {
        var (token, _) = await RegisterAsync();
        Authorize(token);
        var product = await SeedProductAsync();
        var rating = await CreateRatingAsync(product.Id);

        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.DeleteAsync($"/api/ratings/{rating.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WhenNotOwner_Returns403()
    {
        var (ownerToken, _) = await RegisterAsync("owner@example.com", "Owner");
        Authorize(ownerToken);
        var product = await SeedProductAsync();
        var rating = await CreateRatingAsync(product.Id, score: 5);

        var (intruderToken, _) = await RegisterAsync("intruder@example.com", "Intruder");
        Authorize(intruderToken);

        var response = await _client.DeleteAsync($"/api/ratings/{rating.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}