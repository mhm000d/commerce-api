namespace Commerce.Contracts.Carts;

public record AddCartItemRequest(Guid ProductId, int Quantity);

