namespace Commerce.Contracts.Products;

public record ProductsResponse(
    Guid Id,
    string Name,
    decimal Price,
    decimal? AverageRating,
    IReadOnlyList<ProductImageResponse> Images
);