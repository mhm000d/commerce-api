namespace Commerce.Application.Models;

public class CartItem
{
    public Guid Id { get; private set; }
    public Guid CartId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPriceSnapshot { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public Cart Cart { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    // ── Factory ───────────────────────────────────────────────────────────────
    internal static CartItem Create(Guid cartId,
        Guid productId, int quantity, decimal unitPriceSnapshot)
    {
        return new CartItem
        {
            Id = Guid.NewGuid(),
            CartId = cartId,
            ProductId = productId,
            Quantity = quantity,
            UnitPriceSnapshot = unitPriceSnapshot,
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    public void UpdateQuantity(int quantity)
    {
        if (quantity is < 1 or > 999)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        Quantity = quantity;
    }

    public void RefreshPriceSnapshot(decimal currentPrice) => UnitPriceSnapshot = currentPrice;
}