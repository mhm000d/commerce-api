namespace Commerce.Contracts.Products;

public record ProductRequest(
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    string Category,
    IReadOnlyList<ProductSpecification> Specifications,
    string? Slug = null
);