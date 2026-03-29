namespace Commerce.Application.Models;

public class Cart
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public User User { get; private set; } = null!;
    public ICollection<CartItem> Items { get; private set; } = [];

    // ── Factory ───────────────────────────────────────────────────────────────
    public static Cart Create(Guid userId)
    {
        return new Cart
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Adds a new item or increments quantity if the product already exists.
    /// </summary>
    public void AddOrUpdateItem(Guid productId, int quantity, decimal unitPriceSnapshot)
    {
        var existing = Items.FirstOrDefault(i => i.ProductId == productId);

        if (existing is not null)
        {
            existing.UpdateQuantity(existing.Quantity + quantity);
            existing.RefreshPriceSnapshot(unitPriceSnapshot); // ← FIX: was missing
        }
        else
            Items.Add(CartItem.Create(Id, productId, quantity, unitPriceSnapshot));

        Touch();
    }
    
    /// <summary>
    /// Sets an item to an exact quantity.
    /// Also refreshes the price snapshot with the latest product price.
    /// </summary>
    public void UpdateItem(Guid cartItemId, int quantity, decimal unitPriceSnapshot)
    {
        var item = Items.FirstOrDefault(i => i.Id == cartItemId)
                   ?? throw new InvalidOperationException("Cart item not found.");

        item.UpdateQuantity(quantity);
        item.RefreshPriceSnapshot(unitPriceSnapshot);
        Touch();
    }

    public void RemoveItem(Guid cartItemId)
    {
        var item = Items.FirstOrDefault(i => i.Id == cartItemId)
                   ?? throw new InvalidOperationException("Cart item not found.");

        Items.Remove(item);
        Touch();
    }

    public void Clear()
    {
        Items.Clear();
        Touch();
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}