using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Ratings;
using Commerce.Application.Validators;
using Commerce.Tests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Commerce.Tests.IntegrationTests.Services;

public class RatingServiceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private RatingService _ratingService = null!;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync(); // runs Respawn + creates fresh DbContext

        _ratingService = new RatingService(
            dbContext: DbContext,
            ratingValidator: new RatingValidator(),
            logger: Substitute.For<ILogger<RatingService>>()
        );
    }

    // ── Arrange helpers ───────────────────────────────────────────────────────
    private async Task<User> CreateUserAsync(string email = "user@example.com")
    {
        var user = User.Create("Test User", email, "Password1", phone: null);
        await SaveAsync(user);
        return user;
    }
    
    private async Task<Product> CreateProductAsync(int stock = 10)
    {
        // Adjust Product.Create() to match your actual factory signature.
        var product = Product.Create(
            name: "Test Product",
            description: "A product for rating tests.",
            price: 29.99m,
            stockQuantity: stock,
            category: Category.Electronics
        );
        await SaveAsync(product);
        return product;
    }
    
    private async Task SoftDeleteProductAsync(Guid productId)
    {
        // ExecuteUpdateAsync bypasses private setters without exposing
        // a test-only mutation method on the domain model.
        await DbContext.Products
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsDeleted, true)
                .SetProperty(p => p.DeletedAt, DateTimeOffset.UtcNow));
    }

    private async Task SimulatePurchaseAsync(Guid userId, Guid productId)
    {
        var snapshot = AddressSnapshot.From(
            Address.Create(Guid.NewGuid(), "Test User", "01012345678",
                "Egypt", "Cairo", "Nasr City", "Street 9",
                "12", "3", "7", "Home", isDefault: true));
        var order = Order.Create(userId, $"{Random.Shared.Next(1000000):D9}", snapshot);
        var item = OrderItem.Create(order.Id, productId, quantity: 1, unitPrice: 10m);
        order.AddItem(item);
        order.MarkAsPaid();
        await SaveAsync(order);
    }

    private async Task<Rating> CreateRatingWithPurchaseAsync(Guid productId, Guid userId, int score, string? comment)
    {
        await SimulatePurchaseAsync(userId, productId);
        return await _ratingService.CreateRatingAsync(productId, userId, score, comment);
    }
    
    // ── CreateRatingAsync ─────────────────────────────────────────────────────
    [Fact]
    public async Task CreateRating_WithValidData_ShouldPersistRatingAndUpdateProductStats()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync();

        var result = await CreateRatingWithPurchaseAsync(
            productId: product.Id,
            userId: user.Id,
            score: 4,
            comment: "Great product!"
        );
        
        // Returned object
        result.ShouldNotBeNull();
        result.Score.ShouldBe(4);
        result.Comment.ShouldBe("Great product!");
        result.User.ShouldNotBeNull(); // navigation must be loaded for ToResponse()

        // DB: rating row exists
        var savedRating = await DbContext.Ratings
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == result.Id);
        savedRating.ShouldNotBeNull();

        // DB: product stats recalculated
        var updatedProduct = await DbContext.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);
        updatedProduct.RatingCount.ShouldBe(1);
        updatedProduct.AverageRating.ShouldBe(4m);
    }

    [Fact]
    public async Task CreateRating_WithMultipleRatings_ShouldCalculateCorrectAverage()
    {
        // Arrange
        // Two users rating the same product
        var userA = await CreateUserAsync("a@example.com");
        var userB = await CreateUserAsync("b@example.com");
        var product = await CreateProductAsync();

        await CreateRatingWithPurchaseAsync(product.Id, userA.Id, score: 4, comment: null);
        await CreateRatingWithPurchaseAsync(product.Id, userB.Id, score: 2, comment: null);

        // Act
        var updatedProduct = await DbContext.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);

        // Assert
        updatedProduct.RatingCount.ShouldBe(2);
        updatedProduct.AverageRating.ShouldBe(3m); // (4 + 2) / 2
    }
    
    [Fact]
    public async Task CreateRating_WhenProductNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _ratingService.CreateRatingAsync(
            productId: Guid.NewGuid(), // non-existent
            userId: user.Id,
            score: 5,
            comment: null
        );

        await act.ShouldThrowAsync<NotFoundException>();
    }
    
    [Fact]
    public async Task CreateRating_WhenUserAlreadyRatedProduct_ShouldThrowConflictException()
    {
        var user = await CreateUserAsync();
        var product = await CreateProductAsync();

        // First rating succeeds
        await CreateRatingWithPurchaseAsync(product.Id, user.Id, score: 5, comment: null);

        // Second rating on the same product by the same user
        var act = () => CreateRatingWithPurchaseAsync(
            productId: product.Id,
            userId: user.Id,
            score: 3,
            comment: "Changed my mind"
        );

        await act.ShouldThrowAsync<ConflictException>();
    }
    
    // ── UpdateRatingAsync ─────────────────────────────────────────────────────
    [Fact]
    public async Task UpdateRating_WhenRatingNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _ratingService.UpdateRatingAsync(
            ratingId: Guid.NewGuid(), // non-existent
            userId: user.Id,
            score: 3,
            comment: null
        );

        await act.ShouldThrowAsync<NotFoundException>();
    }
    
    // ── DeleteRatingAsync ─────────────────────────────────────────────────────
    [Fact]
    public async Task DeleteRating_ByOwner_ShouldRemoveRatingAndRecalculateStats()
    {
        var userA = await CreateUserAsync("a@example.com");
        var userB = await CreateUserAsync("b@example.com");
        var product = await CreateProductAsync();

        var ratingA = await CreateRatingWithPurchaseAsync(product.Id, userA.Id, score: 4, comment: null);
        await CreateRatingWithPurchaseAsync(product.Id, userB.Id, score: 2, comment: null);

        // Delete user A's rating
        await _ratingService.DeleteRatingAsync(ratingA.Id, userA.Id);

        // Rating row gone
        var exists = await DbContext.Ratings
            .AsNoTracking()
            .AnyAsync(r => r.Id == ratingA.Id);
        exists.ShouldBeFalse();
        
        // Product stats reflect only user B's rating
        var updatedProduct = await DbContext.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);

        updatedProduct.RatingCount.ShouldBe(1);
        updatedProduct.AverageRating.ShouldBe(2m);
    }
    
    [Fact]
    public async Task DeleteRating_WhenLastRating_ShouldResetStatsToZeroAndNull()
    {
        // This tests the edge case in RecalculateProductRatingAsync where
        // GroupBy returns no rows after the last rating is deleted.
        var user = await CreateUserAsync();
        var product = await CreateProductAsync();

        var rating = await CreateRatingWithPurchaseAsync(
            product.Id, user.Id, score: 5, comment: null);

        await _ratingService.DeleteRatingAsync(rating.Id, user.Id);

        var updatedProduct = await DbContext.Products
            .AsNoTracking()
            .SingleAsync(p => p.Id == product.Id);

        updatedProduct.RatingCount.ShouldBe(0);
        updatedProduct.AverageRating.ShouldBeNull();
    }
    
    [Fact]
    public async Task DeleteRating_WhenRatingNotFound_ShouldThrowNotFoundException()
    {
        var user = await CreateUserAsync();

        var act = () => _ratingService.DeleteRatingAsync(
            ratingId: Guid.NewGuid(),
            userId: user.Id
        );

        await act.ShouldThrowAsync<NotFoundException>();
    }
    
    // ── GetRatingsAsync ───────────────────────────────────────────────────────
    [Fact]
    public async Task GetRatings_ShouldReturnAllRatingsForProduct_OrderedByNewest()
    {
        var userA = await CreateUserAsync("a@example.com");
        var userB = await CreateUserAsync("b@example.com");
        var product = await CreateProductAsync();

        var first = await CreateRatingWithPurchaseAsync(product.Id, userA.Id, score: 4, comment: "First");
        var second = await CreateRatingWithPurchaseAsync(product.Id, userB.Id, score: 2, comment: "Second");

        var (ratings, _) = await _ratingService.GetRatingsAsync(product.Id, 1, 10, "newest");

        ratings.Count.ShouldBe(2);
        
        // Ordered by CreatedAt DESC → newest first
        ratings[0].Id.ShouldBe(second.Id);
        ratings[1].Id.ShouldBe(first.Id);

        // User nav loaded — needed for ToResponse()
        ratings.ShouldAllBe(r => r.User != null);
    }
    
    [Fact]
    public async Task GetRatings_WhenNoRatingsExist_ShouldReturnEmptyList()
    {
        var product = await CreateProductAsync();

        var (ratings, _) = await _ratingService.GetRatingsAsync(product.Id, 1, 10, "newest");

        ratings.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRatings_ShouldOnlyReturnRatingsForRequestedProduct()
    {
        var user = await CreateUserAsync();
        var targetProduct = await CreateProductAsync();
        var otherProduct = await CreateProductAsync(); // second product — wrong one

        // Note: same user can't rate both if there were a unique constraint —
        // use two different users to keep the unique index happy.
        var user2 = await CreateUserAsync("user2@example.com");

        await CreateRatingWithPurchaseAsync(targetProduct.Id, user.Id, score: 5, comment: null);
        await CreateRatingWithPurchaseAsync(otherProduct.Id, user2.Id, score: 1, comment: null);

        var (ratings, _) = await _ratingService.GetRatingsAsync(targetProduct.Id, 1, 10, "newest");

        ratings.Count.ShouldBe(1);
        ratings[0].ProductId.ShouldBe(targetProduct.Id);
    }
}