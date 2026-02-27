using System.Collections;

namespace Commerce.Contracts.Products;

public record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    decimal? AverageRating,
    int RatingCount,
    Category Category,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<ProductSpecification> Specifications
);

public record ProductSpecification(string Key, string Value);

public enum Category
{
    Televisions,
    Laptops,
    Games,
    Other
}
