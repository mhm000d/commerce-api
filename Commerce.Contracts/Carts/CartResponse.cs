namespace Commerce.Contracts.Carts;

public record CartResponse(
    Guid Id,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CartItemResponse> Items,
    decimal Subtotal);