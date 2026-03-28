using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Services.Ratings;

public class RatingService(
    AppDbContext dbContext,
    IValidator<Rating> ratingValidator,
    ILogger<RatingService> logger) : IRatingService
{
    public async Task<Rating> CreateRatingAsync(Guid productId, Guid userId, int score, string? comment,
        CancellationToken ct = default)
    {
        var product = await dbContext.Products
                          .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct)
                      ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

        // Check if it's one rating per user per product
        var alreadyRated = await dbContext.Ratings
            .AnyAsync(r => r.UserId == userId && r.ProductId == productId, ct);

        if (alreadyRated)
            throw new ConflictException(
                "You have already rated this product.", "ALREADY_RATED");

        var rating = Rating.Create(productId, userId, score, comment);
        await ratingValidator.ValidateAndThrowAsync(rating, ct);

        // Persist rating + recalculate product stats atomically.
        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        dbContext.Ratings.Add(rating);
        await dbContext.SaveChangesAsync(ct);

        await RecalculateProductRatingAsync(product, ct);
        await dbContext.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        // Load User navigation so ToResponse() can read UserName without a second query
        if (!dbContext.Entry(rating).Reference(r => r.User).IsLoaded)
        {
            await dbContext.Entry(rating).Reference(r => r.User).LoadAsync(ct);
        }

        logger.LogInformation(
            "Rating created. RatingId={RatingId} ProductId={ProductId} UserId={UserId}",
            rating.Id, productId, userId
        );

        return rating;
    }

    public async Task<Rating> UpdateRatingAsync(Guid ratingId, Guid userId, int score, string? comment,
        CancellationToken ct = default)
    {
        var rating = await dbContext.Ratings
                         .Include(r => r.User) // needed for ToResponse()
                         .FirstOrDefaultAsync(r => r.Id == ratingId, ct)
                     ?? throw new NotFoundException("Rating not found.", "RATING_NOT_FOUND");

        // Ownership check — only the author may update
        if (rating.UserId != userId)
            throw new ForbiddenException(
                "You can only edit your own ratings.", "FORBIDDEN");

        rating.Update(score, comment);
        await ratingValidator.ValidateAndThrowAsync(rating, ct);

        var product = await dbContext.Products
                          .FirstOrDefaultAsync(p => p.Id == rating.ProductId, ct)
                      ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        await dbContext.SaveChangesAsync(ct);
        await RecalculateProductRatingAsync(product, ct);
        await dbContext.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        logger.LogInformation(
            "Rating updated. RatingId={RatingId} ProductId={ProductId} UserId={UserId}",
            ratingId, rating.ProductId, userId
        );

        return rating;
    }

    public async Task DeleteRatingAsync(Guid ratingId, Guid userId, CancellationToken ct = default)
    {
        var rating = await dbContext.Ratings
                         .FirstOrDefaultAsync(r => r.Id == ratingId, ct)
                     ?? throw new NotFoundException("Rating not found.", "RATING_NOT_FOUND");

        if (rating.UserId != userId)
            throw new ForbiddenException(
                "You can only delete your own ratings.", "FORBIDDEN");

        var product = await dbContext.Products
                          .FirstOrDefaultAsync(p => p.Id == rating.ProductId, ct)
                      ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

        await using var tx = await dbContext.Database.BeginTransactionAsync(ct);

        dbContext.Ratings.Remove(rating);
        await dbContext.SaveChangesAsync(ct);
        await RecalculateProductRatingAsync(product, ct);
        await dbContext.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        logger.LogInformation(
            "Rating deleted. RatingId={RatingId} ProductId={ProductId} UserId={UserId}",
            ratingId, rating.ProductId, userId);
    }

    public async Task<IReadOnlyList<Rating>> GetRatingsAsync(Guid productId, CancellationToken ct = default)
    {
        return await dbContext.Ratings
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.ProductId == productId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Must be called AFTER the rating row has been inserted/updated/deleted in
    /// the same transaction so the query reflects the latest state.
    /// </summary>
    private async Task RecalculateProductRatingAsync(Product product, CancellationToken ct)
    {
        var stats = await dbContext.Ratings
            .Where(r => r.ProductId == product.Id)
            .GroupBy(r => r.ProductId)
            .Select(g => new
            {
                Count = g.Count(),
                Average = (decimal?)g.Average(r => r.Score)
            })
            .FirstOrDefaultAsync(ct);

        // stats is null when the last rating was just deleted
        product.UpdateRatingStats(
            count: stats?.Count ?? 0,
            average: stats?.Average // null → no ratings left
        );
    }
}