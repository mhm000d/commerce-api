using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Application.Models;
using Commerce.Contracts.Auth;
using Commerce.Contracts.Carts;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

[Collection("Database")]
public sealed class CartApiTests(ApiFactory factory)
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
        string email = "user@example.com", string name = "Test User")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(name, email, "Password1", Phone: null));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var token = body!.AccessToken;

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return token;
    }

    private async Task<Product> SeedProductAsync(int stock = 10, decimal price = 29.99m)
    {
        await using var db = factory.CreateDbContext();
        var product = Product.Create(
            name: "Test Product",
            description: "A product for cart tests.",
            price: price,
            stockQuantity: stock,
            category: Category.Electronics);

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private async Task<CartResponse> AddItemAsync(Guid productId, int quantity = 1)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/cart/items",
            new AddCartItemRequest(productId, quantity));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }

    // ── GET /api/cart ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCart_WhenAuthenticated_Returns200WithEmptyCart()
    {
        await RegisterAndAuthorizeAsync();

        var response = await _client.GetAsync("/api/cart");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.ShouldNotBeNull();
        body.Items.ShouldBeEmpty();
        body.Subtotal.ShouldBe(0m);
    }

    [Fact]
    public async Task GetCart_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/cart");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCart_AfterAddingItems_ReturnsCorrectSubtotal()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 10, price: 20m);

        await AddItemAsync(product.Id, quantity: 3);

        var response = await _client.GetAsync("/api/cart");
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();

        body!.Subtotal.ShouldBe(60m); // 20 * 3
        body.Items.Count.ShouldBe(1);
        body.Items[0].PrimaryImageUrl.ShouldBeNull(); // no images seeded
    }

    // ── POST /api/cart/items ──────────────────────────────────────────────────

    [Fact]
    public async Task PostItem_WithValidData_Returns200WithUpdatedCart()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 5, price: 49.99m);

        var response = await _client.PostAsJsonAsync(
            "/api/cart/items",
            new AddCartItemRequest(product.Id, Quantity: 2));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.ShouldNotBeNull();
        body.Items.Count.ShouldBe(1);
        body.Items[0].Quantity.ShouldBe(2);
        body.Items[0].UnitPriceSnapshot.ShouldBe(49.99m);
        body.Items[0].ProductName.ShouldBe("Test Product");
        body.Subtotal.ShouldBe(99.98m);
    }

    [Fact]
    public async Task PostItem_WhenUnauthenticated_Returns401()
    {
        var product = await SeedProductAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/cart/items",
            new AddCartItemRequest(product.Id, Quantity: 1));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostItem_WhenProductNotFound_Returns404()
    {
        await RegisterAndAuthorizeAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/cart/items",
            new AddCartItemRequest(Guid.NewGuid(), Quantity: 1));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostItem_WhenInsufficientStock_Returns409()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 2);

        var response = await _client.PostAsJsonAsync(
            "/api/cart/items",
            new AddCartItemRequest(product.Id, Quantity: 5));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PostItem_WhenSameProductAddedTwice_ShouldIncrementQuantity()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 10);

        await AddItemAsync(product.Id, quantity: 2);
        var cart = await AddItemAsync(product.Id, quantity: 3);

        // One item, merged quantity
        cart.Items.Count.ShouldBe(1);
        cart.Items[0].Quantity.ShouldBe(5);
    }

    [Fact]
    public async Task PostItem_WhenCombinedQuantityExceedsStock_Returns409()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 3);

        await AddItemAsync(product.Id, quantity: 2);

        var response = await _client.PostAsJsonAsync(
            "/api/cart/items",
            new AddCartItemRequest(product.Id, Quantity: 2));

        // 2 + 2 = 4 > stock of 3
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // ── PUT /api/cart/items/{id} ──────────────────────────────────────────────

    [Fact]
    public async Task PutItem_WithValidQuantity_Returns200WithUpdatedCart()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 10);

        var cartAfterAdd = await AddItemAsync(product.Id, quantity: 2);
        var itemId = cartAfterAdd.Items.Single().Id;

        var response = await _client.PutAsJsonAsync(
            $"/api/cart/items/{itemId}",
            new UpdateCartItemRequest(Quantity: 7));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body!.Items.Single().Quantity.ShouldBe(7);
    }

    [Fact]
    public async Task PutItem_WhenUnauthenticated_Returns401()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/cart/items/{Guid.NewGuid()}",
            new UpdateCartItemRequest(Quantity: 1));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutItem_WhenItemNotFound_Returns404()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync();
        await AddItemAsync(product.Id, quantity: 1); // ensure cart exists

        var response = await _client.PutAsJsonAsync(
            $"/api/cart/items/{Guid.NewGuid()}",
            new UpdateCartItemRequest(Quantity: 1));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PutItem_WhenQuantityExceedsStock_Returns409()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 3);

        var cartAfterAdd = await AddItemAsync(product.Id, quantity: 1);
        var itemId = cartAfterAdd.Items.Single().Id;

        var response = await _client.PutAsJsonAsync(
            $"/api/cart/items/{itemId}",
            new UpdateCartItemRequest(Quantity: 5)); // exceeds stock of 3

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public async Task PutItem_WithInvalidQuantity_Returns400(int invalidQuantity)
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync(stock: 1000);

        var cartAfterAdd = await AddItemAsync(product.Id, quantity: 1);
        var itemId = cartAfterAdd.Items.Single().Id;

        var response = await _client.PutAsJsonAsync(
            $"/api/cart/items/{itemId}",
            new UpdateCartItemRequest(Quantity: invalidQuantity));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── DELETE /api/cart/items/{id} ───────────────────────────────────────────

    [Fact]
    public async Task DeleteItem_ByOwner_Returns204AndItemIsGone()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync();

        var cartAfterAdd = await AddItemAsync(product.Id, quantity: 1);
        var itemId = cartAfterAdd.Items.Single().Id;

        var response = await _client.DeleteAsync($"/api/cart/items/{itemId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify via GET
        var getResponse = await _client.GetAsync("/api/cart");
        var cart = await getResponse.Content.ReadFromJsonAsync<CartResponse>();
        cart!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteItem_WhenUnauthenticated_Returns401()
    {
        var response = await _client.DeleteAsync($"/api/cart/items/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteItem_WhenItemBelongsToAnotherUser_Returns404()
    {
        // User A adds an item
        await RegisterAndAuthorizeAsync("a@example.com", "User A");
        var product = await SeedProductAsync();
        var cartA = await AddItemAsync(product.Id, quantity: 1);
        var itemAId = cartA.Items.Single().Id;

        // User B tries to delete it — sees 404, not 403, to avoid leaking IDs
        _client.DefaultRequestHeaders.Authorization = null;
        await RegisterAndAuthorizeAsync("b@example.com", "User B");

        var response = await _client.DeleteAsync($"/api/cart/items/{itemAId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/cart ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCart_WhenCartHasItems_Returns204AndCartIsEmpty()
    {
        await RegisterAndAuthorizeAsync();
        var product = await SeedProductAsync();
        await AddItemAsync(product.Id, quantity: 1);

        var response = await _client.DeleteAsync("/api/cart");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var cart = await (await _client.GetAsync("/api/cart"))
            .Content.ReadFromJsonAsync<CartResponse>();
        cart!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteCart_WhenNoCartExists_Returns204()
    {
        // Idempotency — user has never touched their cart
        await RegisterAndAuthorizeAsync();

        var response = await _client.DeleteAsync("/api/cart");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCart_WhenUnauthenticated_Returns401()
    {
        var response = await _client.DeleteAsync("/api/cart");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}