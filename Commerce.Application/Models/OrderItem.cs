namespace Commerce.Application.Models;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }

    // ── Computed ──────────────────────────────────────────────────────────────
    public decimal LineTotal => UnitPrice * Quantity;

    // ── Navigation Properties ─────────────────────────────────────────────────
    public Order Order { get; private set; } = null!;

    public Product? Product { get; private set; }

    public static OrderItem Create(Guid orderId, Guid productId, int quantity, decimal unitPrice)
    {
        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }
}