namespace Commerce.Contracts.Products;

public record ProductImageResponse(
    Guid Id,
    Guid ProductId,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder
);