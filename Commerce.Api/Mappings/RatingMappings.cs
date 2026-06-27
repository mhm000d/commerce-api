using Commerce.Application.Models;
using Commerce.Contracts.Ratings;

namespace Commerce.Api.Mappings;

public static class RatingMappings
{
    /// <summary>
    /// RatingService loads UserName before returning from Create and Update.
    /// </summary>
    public static RatingResponse ToResponse(this Rating rating) => new(
        Id:        rating.Id,
        Score:     rating.Score,
        Comment:   rating.Comment,
        CreatedAt: rating.CreatedAt,
        UserName:  rating.User.Name,
        UserId:    rating.UserId
    );
}
