using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Commerce.Application.Models;
using Commerce.Contracts.Auth;
using Commerce.Contracts.Common;
using Commerce.Contracts.Products;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Endpoints;

[Collection("Database")]
public sealed class ProductsApiTests(ApiFactory factory) : IAsyncLifetime
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

    private async Task<Product> SeedProductAsync(
        string name,
        string description,
        decimal price,
        Category category,
        decimal? averageRating = null,
        int ratingCount = 0)
    {
        await using var db = factory.CreateDbContext();

        var product = Product.Create(
            name,
            description,
            price,
            stockQuantity: 10,
            category);

        product.UpdateRatingStats(ratingCount, averageRating);

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private async Task AuthorizeAsAdminAsync()
    {
        await using var db = factory.CreateDbContext();
        var admin = User.Create("Admin", "admin@example.com", "Password1", phone: null);
        admin.PromoteToAdmin();
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("admin@example.com", "Password1"));
        loginResponse.EnsureSuccessStatusCode();

        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.AccessToken);
    }

    // ── GET /api/products ────────────────────────────────────────────────────

    [Fact]
    public async Task GetProducts_ReturnsPagedResponse()
    {
        await SeedProductAsync("Laptop One", "First laptop", 1000m, Category.Laptops);
        await SeedProductAsync("Laptop Two", "Second laptop", 1500m, Category.Laptops);
        await SeedProductAsync("Console", "Gaming console", 500m, Category.Games);

        var response = await _client.GetAsync("/api/v1/products?page=1&pageSize=2");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductsResponse>>();
        body.ShouldNotBeNull();
        body.Data.Count.ShouldBe(2);
        body.Pagination.Page.ShouldBe(1);
        body.Pagination.PageSize.ShouldBe(2);
        body.Pagination.TotalItems.ShouldBe(3);
        body.Pagination.TotalPages.ShouldBe(2);
        body.Pagination.HasNext.ShouldBeTrue();
        body.Pagination.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public async Task GetProducts_WithCategoryAndSort_ReturnsMatchingProducts()
    {
        await SeedProductAsync("Gaming Laptop", "High refresh display", 2000m, Category.Laptops);
        await SeedProductAsync("Office Laptop", "Quiet laptop for work", 1000m, Category.Laptops);
        await SeedProductAsync("Gaming Console", "Living room gaming", 500m, Category.Games);

        var response = await _client.GetAsync(
            "/api/v1/products?category=Laptops&sortBy=price_desc");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductsResponse>>();
        body.ShouldNotBeNull();
        body.Pagination.TotalItems.ShouldBe(2);
        body.Data.Select(p => p.Name).ShouldBe(["Gaming Laptop", "Office Laptop"]);
    }

    [Fact]
    public async Task GetProducts_WithInvalidCategory_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/products?category=Invalid");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetProducts_WithInvalidSort_Returns400()
    {
        var response = await _client.GetAsync("/api/v1/products?sortBy=unknown");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ── GET /api/products/{identifier} ───────────────────────────────────────

    [Fact]
    public async Task GetProduct_WithValidId_ReturnsProduct()
    {
        var product = await SeedProductAsync("Target Product", "A product to find", 99.99m, Category.Laptops);

        var response = await _client.GetAsync($"/api/v1/products/{product.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.ShouldNotBeNull();
        body.Id.ShouldBe(product.Id);
        body.Name.ShouldBe("Target Product");
    }

    [Fact]
    public async Task GetProduct_WithValidSlug_ReturnsProduct()
    {
        var product = await SeedProductAsync("Target Product Two", "Another product", 49.99m, Category.Laptops);

        var response = await _client.GetAsync($"/api/v1/products/{product.Slug}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.ShouldNotBeNull();
        body.Id.ShouldBe(product.Id);
        body.Slug.ShouldBe(product.Slug);
    }

    [Fact]
    public async Task GetProduct_WithNonExistentId_Returns404()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/v1/products/{nonExistentId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProduct_WithNonExistentSlug_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/products/non-existent-slug-12345");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetProduct_AfterAdminUpdate_ReturnsFreshData()
    {
        var product = await SeedProductAsync("Cached Name", "A product to update", 90m, Category.Laptops);

        var firstResponse = await _client.GetAsync($"/api/v1/products/{product.Id}");
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await AuthorizeAsAdminAsync();

        var updateRequest = new ProductRequest(
            Name: "Updated Name",
            Description: "Updated description",
            Price: 95m,
            StockQuantity: 12,
            Category: Category.Laptops.ToString(),
            Specifications: [new Commerce.Contracts.Products.ProductSpecification("Color", "Black")]);

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/admin/products/{product.Id}", updateRequest);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        _client.DefaultRequestHeaders.Authorization = null;

        var secondResponse = await _client.GetAsync($"/api/v1/products/{product.Id}");
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await secondResponse.Content.ReadFromJsonAsync<ProductResponse>();
        body.ShouldNotBeNull();
        body.Name.ShouldBe("Updated Name");
    }
}