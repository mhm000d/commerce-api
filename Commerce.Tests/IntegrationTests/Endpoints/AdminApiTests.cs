using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Application.Models;
using Commerce.Contracts.Auth;
using Commerce.Contracts.Common;
using Commerce.Contracts.Orders;
using Commerce.Contracts.Products;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

[Collection("Database")]
public sealed class AdminApiTests(ApiFactory factory) : IAsyncLifetime
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

    private async Task AuthorizeAsAdminAsync()
    {
        // Seed admin directly — avoids coupling to any auth registration flow
        // that might not support role selection via API.
        await using var db = factory.CreateDbContext();
        var admin = User.Create("Admin", "admin@example.com", "Password1", phone: null);
        admin.PromoteToAdmin();
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin@example.com", "Password1"));
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private async Task AuthorizeAsCustomerAsync(
        string email = "cust@example.com", string name = "Customer")
    {
        await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(name, email, "Password1", null));
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Password1"));
        login.EnsureSuccessStatusCode();

        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    private async Task<Order> SeedOrderAsync(OrderStatus status = OrderStatus.Placed)
    {
        await using var db = factory.CreateDbContext();

        var user = User.Create($"User {Guid.NewGuid()}", $"{Guid.NewGuid()}@example.com",
            "Password1", null);
        db.Users.Add(user);

        var product = Product.Create("Product", "Desc", 50m, 10, Category.Electronics);
        db.Products.Add(product);

        var snapshot = AddressSnapshot.From(
            Address.Create(user.Id, "Test", "01012345678",
                "Egypt", "Cairo", "Nasr City", "Street 9",
                "12", "3", "7", "Home", true));

        var order = Order.Create(user.Id, $"{Random.Shared.Next(1000000):D9}", snapshot);
        var item  = OrderItem.Create(order.Id, product.Id, 1, 50m);
        order.AddItem(item);
        order.SetTotalAmount(50m);
        product.DecreaseStock(1);

        if (status >= OrderStatus.Paid)    order.MarkAsPaid();
        if (status >= OrderStatus.Shipped) order.MarkAsShipped();

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static ProductRequest ValidProductRequest() =>
        new(
            "Admin Product",
            "Created through the admin API.",
            25m,
            10,
            Category.Electronics.ToString(),
            [new Commerce.Contracts.Products.ProductSpecification("Color", "Black")]);

    private static HttpRequestMessage CreateProductAdminRequest(string method, string path)
    {
        var httpMethod = new HttpMethod(method);
        var request = new HttpRequestMessage(httpMethod, path);

        if (httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put)
            request.Content = JsonContent.Create(ValidProductRequest());

        return request;
    }

    private static HttpRequestMessage CreateProductImageAdminRequest(string method, string path)
    {
        var httpMethod = new HttpMethod(method);
        var request = new HttpRequestMessage(httpMethod, path);

        if (httpMethod == HttpMethod.Post)
        {
            var content = new MultipartFormDataContent();
            var image = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
            image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(image, "image", "test.png");
            request.Content = content;
        }

        return request;
    }

    // ── Product admin authorization ───────────────────────────────────────────

    [Theory]
    [InlineData("POST", "/api/admin/products")]
    [InlineData("PUT", "/api/admin/products/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/admin/products/00000000-0000-0000-0000-000000000001")]
    public async Task ProductAdminEndpoints_WhenUnauthenticated_Return401(
        string method,
        string path)
    {
        using var request = CreateProductAdminRequest(method, path);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST", "/api/admin/products")]
    [InlineData("PUT", "/api/admin/products/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/admin/products/00000000-0000-0000-0000-000000000001")]
    public async Task ProductAdminEndpoints_WhenCustomer_Return403(
        string method,
        string path)
    {
        await AuthorizeAsCustomerAsync();
        using var request = CreateProductAdminRequest(method, path);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Product image admin authorization ─────────────────────────────────────

    [Theory]
    [InlineData("POST", "/api/admin/products/00000000-0000-0000-0000-000000000001/images")]
    [InlineData("DELETE", "/api/admin/products/00000000-0000-0000-0000-000000000001/images/00000000-0000-0000-0000-000000000002")]
    [InlineData("PUT", "/api/admin/products/00000000-0000-0000-0000-000000000001/images/00000000-0000-0000-0000-000000000002/set-primary")]
    public async Task ProductImageAdminEndpoints_WhenUnauthenticated_Return401(
        string method,
        string path)
    {
        using var request = CreateProductImageAdminRequest(method, path);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("POST", "/api/admin/products/00000000-0000-0000-0000-000000000001/images")]
    [InlineData("DELETE", "/api/admin/products/00000000-0000-0000-0000-000000000001/images/00000000-0000-0000-0000-000000000002")]
    [InlineData("PUT", "/api/admin/products/00000000-0000-0000-0000-000000000001/images/00000000-0000-0000-0000-000000000002/set-primary")]
    public async Task ProductImageAdminEndpoints_WhenCustomer_Return403(
        string method,
        string path)
    {
        await AuthorizeAsCustomerAsync();
        using var request = CreateProductImageAdminRequest(method, path);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── GET /api/admin/orders ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminOrders_WhenAdmin_Returns200WithAllOrders()
    {
        await AuthorizeAsAdminAsync();
        await SeedOrderAsync();
        await SeedOrderAsync();

        var response = await _client.GetAsync("/api/admin/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content
            .ReadFromJsonAsync<PagedResponse<OrderSummaryResponse>>();
        body!.Pagination.TotalItems.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetAdminOrders_WhenNotAdmin_Returns403()
    {
        await AuthorizeAsCustomerAsync();

        var response = await _client.GetAsync("/api/admin/orders");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAdminOrders_WhenUnauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/admin/orders");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── PUT /api/admin/orders/{id}/status ─────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_ValidTransition_Returns200WithNewStatus()
    {
        await AuthorizeAsAdminAsync();
        var order = await SeedOrderAsync(OrderStatus.Placed);

        var response = await _client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new UpdateOrderStatusRequest("Paid"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<OrderResponse>();
        body!.Status.ShouldBe("Paid");
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidStatusString_Returns400()
    {
        await AuthorizeAsAdminAsync();
        var order = await SeedOrderAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new UpdateOrderStatusRequest("FlyingPigs"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateStatus_InvalidTransition_Returns409()
    {
        await AuthorizeAsAdminAsync();
        // PLACED → DELIVERED is not a valid state machine transition
        var order = await SeedOrderAsync(OrderStatus.Placed);

        var response = await _client.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new UpdateOrderStatusRequest("Delivered"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateStatus_WhenUnauthenticated_Returns401()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/admin/orders/{Guid.NewGuid()}/status",
            new UpdateOrderStatusRequest("Paid"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateStatus_WhenNotAdmin_Returns403()
    {
        await AuthorizeAsCustomerAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/admin/orders/{Guid.NewGuid()}/status",
            new UpdateOrderStatusRequest("Paid"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
