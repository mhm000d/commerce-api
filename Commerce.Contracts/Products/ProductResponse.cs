using System.Collections;
using Commerce.Contracts.ProductImages;

namespace Commerce.Contracts.Products;

public record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    decimal? AverageRating,
    int RatingCount,
    string Category,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<ProductSpecification> Specifications
);

public record ProductSpecification(string Key, string Value);

// public enum Category
// {
//     Televisions,
//     Laptops,
//     Games,
//     Other
// }
