namespace Commerce.Contracts.Products;

public record ProductImageResponse(
    Guid Id,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder
);