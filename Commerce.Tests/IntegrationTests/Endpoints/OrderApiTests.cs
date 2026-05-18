using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Application.Database;
using Commerce.Application.Models;
using Commerce.Application.Services.Carts;
using Commerce.Application.Services.Payments;
using Commerce.Application.Validators;
using Commerce.Contracts.Auth;
using Commerce.Contracts.Carts;
using Commerce.Contracts.Common;
using Commerce.Contracts.Orders;
using Commerce.Tests.IntegrationTests.Infrastructure;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

[Collection("Database")]
public sealed class OrderApiTests(ApiFactory factory) : IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _client = factory.CreateClient();
        
        
        
        // Default Stripe mock for all card tests
        factory.StripeMock
            .CreateCheckoutSessionAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IEnumerable<CheckoutLineItem>>(), Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(("cs_test_session", "cs_test_secret"));
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> RegisterAndAuthorizeAsync(
        string email = "user@example.com", string role = "Customer")
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email, "Password1", Phone: null));

        response.EnsureSuccessStatusCode();
        var body  = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var token = body!.AccessToken;

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return token;
    }

    private async Task<Product> SeedProductAsync(decimal price = 50m, int stock = 10)
    {
        await using var db = factory.CreateDbContext();
        var product = Product.Create("Test Product", "Desc", price, stock, Category.Electronics);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private async Task<Address> SeedAddressAsync(Guid userId)
    {
        await using var db = factory.CreateDbContext();
        var address = Address.Create(userId, "John Doe", "01012345678",
            "Egypt", "Cairo", "Nasr City", "Street 9", "12", "3", "7", "Home", true);
        db.Addresses.Add(address);
        await db.SaveChangesAsync();
        return address;
    }

    private async Task AddToCartAsync(Guid productId, int quantity = 1)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/cart/items",
            new AddCartItemRequest(productId, quantity));
        response.EnsureSuccessStatusCode();
    }

    private Guid GetUserIdFromToken(string token)
    {
        // Decode JWT sub claim
        var parts  = token.Split('.');
        var payload = System.Text.Json.JsonDocument.Parse(
            System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(parts[1].PadRight((parts[1].Length + 3) & ~3, '='))));
        return Guid.Parse(payload.RootElement.GetProperty("sub").GetString()!);
    }

    // ── POST /api/checkout ────────────────────────────────────────────────────

    [Fact]
    public async Task Checkout_COD_Returns201WithNullClientSecret()
    {
        var token   = await RegisterAndAuthorizeAsync();
        var userId  = GetUserIdFromToken(token);
        var product = await SeedProductAsync();
        var address = await SeedAddressAsync(userId);
        await AddToCartAsync(product.Id);

        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest(address.Id, "CashOnDelivery"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        body.ShouldNotBeNull();
        body.StripeClientSecret.ShouldBeNull();
        body.OrderNumber.ShouldNotBeNullOrEmpty();
        body.OrderNumber.ShouldNotContain("Order #");
        body.OrderNumber.All(char.IsDigit).ShouldBeTrue();
        body.TotalAmount.ShouldBe(50m);
    }

    [Fact]
    public async Task Checkout_Card_Returns201WithClientSecret()
    {
        var token   = await RegisterAndAuthorizeAsync();
        var userId  = GetUserIdFromToken(token);
        var product = await SeedProductAsync();
        var address = await SeedAddressAsync(userId);
        await AddToCartAsync(product.Id);

        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest(address.Id, "Card"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CheckoutResponse>();
        body!.StripeClientSecret.ShouldBe("cs_test_secret");
    }

    [Fact]
    public async Task Checkout_WhenUnauthenticated_Returns401()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest(Guid.NewGuid(), "CashOnDelivery"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Checkout_WhenInvalidPaymentMethod_Returns400()
    {
        await RegisterAndAuthorizeAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest(Guid.NewGuid(), "BitcoinOrSomething"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Checkout_WhenCartIsEmpty_Returns400()
    {
        var token   = await RegisterAndAuthorizeAsync();
        var userId  = GetUserIdFromToken(token);
        var address = await SeedAddressAsync(userId);
        
        await _client.GetAsync("/api/cart");
        
        var response = await _client.PostAsJsonAsync(
            "/api/checkout",
            new CheckoutRequest(address.Id, "CashOnDelivery"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── GET /api/orders ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrders_WhenAuthenticated_Returns200WithPaginatedList()
    {
        var token   = await RegisterAndAuthorizeAsync();
        var userId  = GetUserIdFromToken(token);
        var product = await SeedProductAsync();
        var address = await SeedAddressAsync(userId);
        await AddToCartAsync(product.Id);
        await _client.PostAsJsonAsync("/api/checkout",
            new CheckoutRequest(address.Id, "CashOnDelivery"));

        var response = await _client.GetAsync("/api/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content
            .ReadFromJsonAsync<PagedResponse<OrderSummaryResponse>>();
        body!.Pagination.TotalItems.ShouldBe(1);
        body.Data.Single().TotalAmount.ShouldBe(50m);
    }

    [Fact]
    public async Task GetOrders_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/orders");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/orders/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetOrder_WhenOwner_Returns200WithFullDetail()
    {
        var token   = await RegisterAndAuthorizeAsync();
        var userId  = GetUserIdFromToken(token);
        var product = await SeedProductAsync();
        var address = await SeedAddressAsync(userId);
        await AddToCartAsync(product.Id);

        var checkoutResponse = await (await _client.PostAsJsonAsync(
            "/api/checkout", new CheckoutRequest(address.Id, "CashOnDelivery")))
            .Content.ReadFromJsonAsync<CheckoutResponse>();

        var response = await _client.GetAsync($"/api/orders/{checkoutResponse!.OrderId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.Items.ShouldNotBeEmpty();
        body.Items[0].ProductName.ShouldBe("Test Product");
        body.ShippingAddress.FullName.ShouldBe("John Doe");
    }

    [Fact]
    public async Task GetOrder_WhenNotOwner_Returns404()
    {
        // User A places order
        await RegisterAndAuthorizeAsync("a@example.com");
        var userAId = GetUserIdFromToken(
            (await (await _client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest("a@example.com", "Password1")))
                .Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);

        var product  = await SeedProductAsync();
        var addressA = await SeedAddressAsync(userAId);
        await AddToCartAsync(product.Id);
        var checkout = await (await _client.PostAsJsonAsync(
            "/api/checkout", new CheckoutRequest(addressA.Id, "CashOnDelivery")))
            .Content.ReadFromJsonAsync<CheckoutResponse>();

        // User B tries to view it
        _client.DefaultRequestHeaders.Authorization = null;
        await RegisterAndAuthorizeAsync("b@example.com");

        var response = await _client.GetAsync($"/api/orders/{checkout!.OrderId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ── POST /api/orders/{id}/cancel ──────────────────────────────────────────

    [Fact]
    public async Task CancelOrder_WhenPlaced_Returns200AndOrderIsCancelled()
    {
        var token   = await RegisterAndAuthorizeAsync();
        var userId  = GetUserIdFromToken(token);
        var product = await SeedProductAsync(stock: 10);
        var address = await SeedAddressAsync(userId);
        await AddToCartAsync(product.Id);

        var checkout = await (await _client.PostAsJsonAsync(
            "/api/checkout", new CheckoutRequest(address.Id, "CashOnDelivery")))
            .Content.ReadFromJsonAsync<CheckoutResponse>();

        var response = await _client.PostAsync(
            $"/api/orders/{checkout!.OrderId}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.Status.ShouldBe("Cancelled");
    }

    // ── GET /api/checkout/session-status ──────────────────────────────────────

    [Fact]
    public async Task GetCheckoutSessionStatus_Returns200WithStatusFromStripe()
    {
        await RegisterAndAuthorizeAsync();
        
        factory.StripeMock
            .GetSessionStatusAsync("cs_test_session", Arg.Any<CancellationToken>())
            .Returns(new StripeSessionStatus("complete", "customer@example.com"));

        var response = await _client.GetAsync("/api/checkout/session-status?sessionId=cs_test_session");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CheckoutSessionStatusResponse>();
        body!.Status.ShouldBe("complete");
        body.CustomerEmail.ShouldBe("customer@example.com");
    }

    [Fact]
    public async Task GetCheckoutSessionStatus_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/checkout/session-status?sessionId=any");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CancelOrder_WhenUnauthenticated_Returns401()
    {
        var response = await _client.PostAsync(
            $"/api/orders/{Guid.NewGuid()}/cancel", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
