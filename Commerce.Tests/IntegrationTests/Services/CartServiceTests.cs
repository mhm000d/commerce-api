using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Carts;
using Commerce.Application.Validators;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Services;

public class CartServiceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private CartService _cartService = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        _cartService = new CartService(
            dbContext: DbContext,
            cartValidator: new CartValidator(),
            logger: Substitute.For<ILogger<CartService>>()
        );
    }

    // ── Arrange helpers ───────────────────────────────────────────────────────

    private async Task<User> CreateUserAsync(string email = "user@example.com")
    {
        var user = User.Create("Test User", email, "Password1", phone: null);
        await SaveAsync(user);
        return user;
    }

    private async Task<Product> CreateProductAsync(
        decimal price = 29.99m, int stock = 10)
    {
        var product = Product.Create(
            name: "Test Product",
            description: "A product for cart tests.",
            price: price,
            stockQuantity: stock,
            category: Category.Electronics
        );
        await SaveAsync(product);
        return product;
    }

    private async Task SoftDeleteProductAsync(Guid productId)
    {
        await DbContext.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.DeletedAt, DateTimeOffset.UtcNow));
    }

    private async Task SetStockAsync(Guid productId, int stock)
    {
        await DbContext.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, stock));
    }

    // ── GetOrCreateCartAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetOrCreateCart_WhenNoCartExists_ShouldCreateAndPersistCart()
    {
        var user = await CreateUserAsync();

        var cart = await _cartService.GetOrCreateCartAsync(user.Id);

        cart.ShouldNotBeNull();
        cart.UserId.ShouldBe(user.Id);
        cart.Items.ShouldBeEmpty();

        var persisted = await DbContext.Carts
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.UserId == user.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetOrCreateCart_WhenCartAlreadyExists_ShouldReturnExistingCart()
    {
        var user = await CreateUserAsync();

        var first = await _cartService.GetOrCreateCartAsync(user.Id);
        var second = await _cartService.GetOrCreateCartAsync(user.Id);

        // Same cart, no duplicate rows
        second.Id.ShouldBe(first.Id);

        var count = await DbContext.Carts.CountAsync(c => c.UserId == user.Id);
        count.ShouldBe(1);
    }

    // ── AddItemAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItem_WithValidData_ShouldPersistItemWithPriceSnapshot()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(price: 49.99m, stock: 5);

        var cart = await _cartService.AddItemAsync(user.Id, product.Id, quantity: 2);

        cart.Items.Count.ShouldBe(1);

        var item = cart.Items.Single();
        item.ProductId.ShouldBe(product.Id);
        item.Quantity.ShouldBe(2);
        item.UnitPriceSnapshot.ShouldBe(49.99m); // snapshot from Product.Price

        // Verify Product nav is loaded (required by ToResponse())
        item.Product.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddItem_WhenProductAlreadyInCart_ShouldIncrementQuantity()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(stock: 10);

        await _cartService.AddItemAsync(user.Id, product.Id, quantity: 2);
        var cart = await _cartService.AddItemAsync(user.Id, product.Id, quantity: 3);

        // Should merge into a single item with combined quantity
        cart.Items.Count.ShouldBe(1);
        cart.Items.Single().Quantity.ShouldBe(5);
    }

    [Fact]
    public async Task AddItem_WhenProductAlreadyInCart_ShouldRefreshPriceSnapshot()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(price: 10m, stock: 10);

        await _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);

        // Simulate a price change
        await DbContext.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 20m));

        var cart = await _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);

        // Snapshot must reflect the new price, not the old one
        cart.Items.Single().UnitPriceSnapshot.ShouldBe(20m);
    }

    [Fact]
    public async Task AddItem_WhenProductNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _cartService.AddItemAsync(user.Id, Guid.NewGuid(), quantity: 1);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddItem_WhenProductSoftDeleted_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync();
        await SoftDeleteProductAsync(product.Id);

        var act = () => _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddItem_WhenRequestedQuantityExceedsStock_ShouldThrowConflictException()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(stock: 3);

        var act = () => _cartService.AddItemAsync(user.Id, product.Id, quantity: 4);

        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AddItem_WhenExistingPlusNewExceedsStock_ShouldThrowConflictException()
    {
        // Stock = 3, cart already has 2 → adding 2 more must fail even though
        // 2 alone would pass the naive stock check.
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(stock: 3);

        await _cartService.AddItemAsync(user.Id, product.Id, quantity: 2);

        var act = () => _cartService.AddItemAsync(user.Id, product.Id, quantity: 2);

        await act.ShouldThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task AddItem_WhenTotalQuantityExceeds999_ShouldThrowValidationException()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(stock: 1000);

        await _cartService.AddItemAsync(user.Id, product.Id, quantity: 999);

        // Adding even 1 more would push it to 1000 — domain cap exceeded
        var act = () => _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);

        await act.ShouldThrowAsync<ValidationException>();
    }

    // ── UpdateItemAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateItem_WithValidQuantity_ShouldSetExactQuantityAndRefreshPrice()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(price: 10m, stock: 10);

        var cartAfterAdd = await _cartService.AddItemAsync(user.Id, product.Id, quantity: 2);
        var itemId = cartAfterAdd.Items.Single().Id;

        // Simulate price change between add and update
        await DbContext.Products
            .Where(p => p.Id == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 15m));

        var cart = await _cartService.UpdateItemAsync(user.Id, itemId, quantity: 5);

        var item = cart.Items.Single();
        item.Quantity.ShouldBe(5);           // set, not incremented
        item.UnitPriceSnapshot.ShouldBe(15m); // refreshed
    }

    [Fact]
    public async Task UpdateItem_WhenCartNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _cartService.UpdateItemAsync(user.Id, Guid.NewGuid(), quantity: 1);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateItem_WhenCartItemNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync();
        await _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);

        var act = () => _cartService.UpdateItemAsync(user.Id, Guid.NewGuid(), quantity: 1);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateItem_WhenNewQuantityExceedsStock_ShouldThrowConflictException()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(stock: 3);

        var cartAfterAdd = await _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);
        var itemId = cartAfterAdd.Items.Single().Id;

        // Reduce stock after the item was added
        await SetStockAsync(product.Id, 2);

        var act = () => _cartService.UpdateItemAsync(user.Id, itemId, quantity: 3);

        await act.ShouldThrowAsync<ConflictException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task UpdateItem_WithInvalidQuantity_ShouldThrowValidationException(int invalidQuantity)
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync(stock: 1000);

        var cartAfterAdd = await _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);
        var itemId = cartAfterAdd.Items.Single().Id;

        var act = () => _cartService.UpdateItemAsync(user.Id, itemId, quantity: invalidQuantity);

        await act.ShouldThrowAsync<ValidationException>();
    }

    // ── RemoveItemAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveItem_ByOwner_ShouldDeleteItemFromCart()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync();

        var cartAfterAdd = await _cartService.AddItemAsync(user.Id, product.Id, quantity: 1);
        var itemId = cartAfterAdd.Items.Single().Id;

        await _cartService.RemoveItemAsync(user.Id, itemId);

        var cart = await DbContext.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .SingleAsync(c => c.UserId == user.Id);

        cart.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveItem_WhenCartNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _cartService.RemoveItemAsync(user.Id, Guid.NewGuid());

        await act.ShouldThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RemoveItem_WhenItemNotInUsersCart_ShouldThrowNotFoundException()
    {
        // Ensures a user can't delete another user's cart item by guessing its ID.
        var userA = await CreateUserAsync("a@example.com");
        var userB = await CreateUserAsync("b@example.com");
        var product = await CreateProductAsync();

        var cartB = await _cartService.AddItemAsync(userB.Id, product.Id, quantity: 1);
        var itemBId = cartB.Items.Single().Id;

        // User A tries to remove user B's item
        var act = () => _cartService.RemoveItemAsync(userA.Id, itemBId);

        await act.ShouldThrowAsync<NotFoundException>();
    }

    // ── ClearCartAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearCart_WhenCartHasItems_ShouldRemoveAllItems()
    {
        var user = await CreateUserAsync();
        var productA = await CreateProductAsync(stock: 10);
        var productB = await CreateProductAsync(stock: 10);

        await _cartService.AddItemAsync(user.Id, productA.Id, quantity: 1);
        await _cartService.AddItemAsync(user.Id, productB.Id, quantity: 2);

        await _cartService.ClearCartAsync(user.Id);

        var cart = await DbContext.Carts
            .AsNoTracking()
            .Include(c => c.Items)
            .SingleAsync(c => c.UserId == user.Id);

        cart.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClearCart_WhenNoCartExists_ShouldCompleteWithoutError()
    {
        // Idempotency — safe to call even if the user has never opened their cart.
        var user = await CreateUserAsync();

        var act = () => _cartService.ClearCartAsync(user.Id);

        await act.ShouldNotThrowAsync();
    }
}