namespace Commerce.Contracts.ProductImages;

public record ProductImageResponse(
    Guid Id,
    Guid ProductId,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder
);