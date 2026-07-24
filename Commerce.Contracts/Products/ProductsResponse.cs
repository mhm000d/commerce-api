using Commerce.Contracts.ProductImages;

namespace Commerce.Contracts.Products;

public record ProductsResponse(
    Guid Id,
    string Slug,
    string Name,
    decimal Price,
    decimal? AverageRating,
    IReadOnlyList<ProductImageResponse> Images
);