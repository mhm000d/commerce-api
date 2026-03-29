namespace Commerce.Contracts.Carts;

public record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string? PrimaryImageUrl,
    int Quantity,
    decimal UnitPriceSnapshot);