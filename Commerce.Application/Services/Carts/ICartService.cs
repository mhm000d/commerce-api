using Commerce.Application.Models;

namespace Commerce.Application.Services.Carts;

public interface ICartService
{
    /// <summary>Returns the user's cart, creating an empty one if it doesn't exist yet.</summary>
    Task<Cart> GetOrCreateCartAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adds quantity to the item if the product is already in the cart,
    /// otherwise creates a new item. Soft stock validation runs here.
    /// </summary>
    Task<Cart> AddItemAsync(Guid userId, Guid productId, int quantity, CancellationToken ct = default);

    /// <summary>Sets the item to an exact quantity. Soft stock validation runs here.</summary>
    Task<Cart> UpdateItemAsync(Guid userId, Guid cartItemId, int quantity, CancellationToken ct = default);

    Task RemoveItemAsync(Guid userId, Guid cartItemId, CancellationToken ct = default);

    /// <summary>Clears all items. Idempotent — safe to call even if no cart exists.</summary>
    Task ClearCartAsync(Guid userId, CancellationToken ct = default);
}