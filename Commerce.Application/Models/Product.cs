namespace Commerce.Application.Models;

public class Product
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    
    public required decimal Price { get; init; }
    public required int StockQuantity { get; init; }
    
    public decimal? AverageRating { get; init; }
    public int RatingCount { get; init; }
    
    public required Category Category { get; init; }
    
    public List<ProductSpecification> Specifications { get; init; } = [];
    
    public bool IsDeleted { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    
    public DateTimeOffset CreatedAt { get; init; }
}

public record ProductSpecification(string Key, string Value);

public enum Category
{
    Televisions,
    Laptops,
    Games
}
