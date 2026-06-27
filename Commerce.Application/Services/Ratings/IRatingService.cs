using Commerce.Application.Models;

namespace Commerce.Application.Services.Ratings;

public interface IRatingService
{
    Task<Rating> CreateRatingAsync(
        Guid productId, Guid userId, int score, string? comment,
        CancellationToken ct = default);

    Task<Rating> UpdateRatingAsync(
        Guid ratingId, Guid userId, int score, string? comment,
        CancellationToken ct = default);

    Task DeleteRatingAsync(
        Guid ratingId, Guid userId,
        CancellationToken ct = default);

    Task<(IReadOnlyList<Rating> Ratings, int TotalCount)> GetRatingsAsync(
        Guid productId,
        int page,
        int pageSize,
        string? sortBy,
        CancellationToken ct = default);
}