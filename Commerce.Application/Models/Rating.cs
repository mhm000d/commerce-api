namespace Commerce.Application.Models;

public class Rating
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }
    public int Score { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public Product Product { get; private set; } = null!;
    public User User { get; private set; } = null!;

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Rating Create(Guid productId, Guid userId, int score, string? comment = null)
    {
        return new Rating
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            UserId = userId,
            Score = score,
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    /// <summary>
    /// After calling this, the service must recalculate Product.AverageRating
    /// and Product.RatingCount in the same transaction.
    /// </summary>
    public void Update(int score, string? comment)
    {
        Score = score;
        Comment = comment;
    }
}