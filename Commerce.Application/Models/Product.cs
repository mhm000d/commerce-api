namespace Commerce.Application.Models;

public class Product
{
    public required Guid Id { get; init; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal Price { get; set; }
    public required int StockQuantity { get; set; }
    public decimal? AverageRating { get; init; }
    public int RatingCount { get; init; }
    public required Category Category { get; set; }
    public ICollection<ProductSpecification> Specifications { get; set; } = [];
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public ICollection<ProductImage> Images { get; private set; } = [];
    
    // ── Model Behaviour ─────────────────────────────────────────────────
    public void UpdateDetails(string name, string description, decimal price, int stockQuantity, Category category)
    {
        Name = name;
        Description = description;
        Price = price;
        StockQuantity = stockQuantity;
        Category = category;
    }
    public void SetSpecifications(IEnumerable<ProductSpecification> specs)
    {
        Specifications.Clear();  // Remove old ones
        foreach (var spec in specs)
            Specifications.Add(new ProductSpecification(spec.Key, spec.Value));
    }
    public void Delete()
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}

public record ProductSpecification(string Key, string Value);

public enum Category
{
    Televisions,
    Laptops,
    Games,
    Other
}