namespace Commerce.Contracts.Ratings;

public record RatingResponse(
    Guid Id,
    int Score,
    string? Comment,
    DateTimeOffset CreatedAt,
    string UserName
);

