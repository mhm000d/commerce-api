using NpgsqlTypes;
using System.Text.RegularExpressions;

namespace Commerce.Application.Models;

public partial class Product
{
    public required Guid Id { get; init; }
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }
    public required int StockQuantity { get; set; }
    public decimal? AverageRating { get; set; }
    public int RatingCount { get; set; }
    public required Category Category { get; set; }
    public ICollection<ProductSpecification> Specifications { get; set; } = [];
    public NpgsqlTsVector? SearchVector { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public ICollection<ProductImage> Images { get; private set; } = [];
    public ICollection<Rating> Ratings { get; private set; } = [];
    public ICollection<CartItem> CartItems { get; private set; } = [];
    public ICollection<OrderItem> OrderItems { get; private set; } = [];

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Product Create(
        string name,
        string? description,
        decimal price,
        int stockQuantity,
        Category category,
        string? slug = null)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Slug = slug ?? GenerateDefaultSlug(name),
            Name = name,
            Description = description!,
            Price = price,
            StockQuantity = stockQuantity,
            Category = category,
            RatingCount = 0,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ── Model Behaviour ─────────────────────────────────────────────────
    public void UpdateDetails(string name, string description, decimal price, int stockQuantity, Category category)
    {
        Name = name;
        Description = description;
        Price = price;
        StockQuantity = stockQuantity;
        Category = category;
    }

    public void UpdateSlug(string slug)
    {
        Slug = slug;
    }

    public static string NormalizeSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // 1. Replace non-alphanumeric (except space and hyphen) with empty string
        var clean = SlugRegex().Replace(value, string.Empty);

        // 2. Convert to lower case and replace spaces with hyphens
        var slugged = clean.ToLowerInvariant().Replace(" ", "-");

        // 3. Replace multiple hyphens with a single one and trim
        return MultipleHyphensRegex().Replace(slugged, "-").Trim('-');
    }

    private static string GenerateDefaultSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return $"product-{Guid.NewGuid():N}";

        return $"{NormalizeSlug(value)}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    [GeneratedRegex("[^0-9A-Za-z _-]", RegexOptions.NonBacktracking)]
    private static partial Regex SlugRegex();

    [GeneratedRegex("-+", RegexOptions.NonBacktracking)]
    private static partial Regex MultipleHyphensRegex();

    public void SetSpecifications(IEnumerable<ProductSpecification> specs)
    {
        Specifications.Clear(); // Remove old ones
        foreach (var spec in specs)
            Specifications.Add(new ProductSpecification(spec.Key, spec.Value));
    }

    public void Delete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Called by RatingService after every rating create / update / delete.
    /// Must be called inside the same transaction as the rating change.
    /// </summary>
    public void UpdateRatingStats(int count, decimal? average)
    {
        RatingCount = count;
        AverageRating = average;
    }
    
    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (StockQuantity < quantity)
            throw new InvalidOperationException(
                $"Cannot decrease stock for '{Name}': only {StockQuantity} available.");

        StockQuantity -= quantity;
    }

    /// <summary>
    /// Restores stock on order cancellation or payment failure.
    /// </summary>
    public void RestoreStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        StockQuantity += quantity;
    }
}

public record ProductSpecification(string Key, string Value);

public enum Category
{
    Mobiles,
    Laptops,
    Televisions,
    Games,
    Appliances,
    Electronics,
    Home,
    Other
}
