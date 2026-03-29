using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ValidationException = Commerce.Application.Exceptions.ValidationException;

namespace Commerce.Application.Services.Carts;

public class CartService(
    AppDbContext dbContext,
    IValidator<Cart> cartValidator,
    ILogger<CartService> logger) : ICartService
{
    public async Task<Cart> GetOrCreateCartAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await dbContext.Carts
            .Include(c => c.Items)
            .ThenInclude(i => i.Product)
            .ThenInclude(p => p.Images.Where(img => img.IsPrimary))
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        if (cart is not null)
            return cart;

        // First time the user touches their cart — create it silently.
        cart = Cart.Create(userId);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Cart created. UserId={UserId}", userId);
        return cart;
    }

    public async Task<Cart> AddItemAsync(Guid userId, Guid productId, int quantity, CancellationToken ct = default)
    {
        // Validate product is active.
        var product = await dbContext.Products
                          .AsNoTracking()
                          .Include(p => p.Images.Where(img => img.IsPrimary))
                          .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct)
                      ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

        // Load or lazily create the cart.
        //    We need Items loaded so AddOrUpdateItem can find an existing entry.
        var cart = await dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);
        
        if (cart is null)
        {
            cart = Cart.Create(userId);
            dbContext.Carts.Add(cart);
        }
        
        // Stock check must account for what's already in the cart.
        //    e.g. stock = 3, cart has 2 → requesting 2 more must fail.
        var existing = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        var totalQuantity = (existing?.Quantity ?? 0) + quantity;

        if (product.StockQuantity < totalQuantity)
            throw new ConflictException(
                $"Insufficient stock. Available: {product.StockQuantity}.",
                "INSUFFICIENT_STOCK");
        
        // Guard against the 999 cap *before* calling domain, because
        // CartItem.UpdateQuantity throws ArgumentOutOfRangeException internally
        // which would bubble up as 500 before FluentValidation ever runs.
        if (totalQuantity > 999)
            throw new ValidationException(
                "Total quantity for this item cannot exceed 999.",
                "QUANTITY_LIMIT_EXCEEDED");

        // Mutate through the domain model (also refreshes price snapshot).
        cart.AddOrUpdateItem(productId, quantity, product.Price);

        await cartValidator.ValidateAndThrowAsync(cart, ct);

        await dbContext.SaveChangesAsync(ct);
        
        // Load Product navigation on every item so ToResponse() can read ProductName and ProductImageUrl.
        await LoadProductNavigationsAsync(cart, ct);

        logger.LogInformation(
            "Cart item added. UserId={UserId} ProductId={ProductId} Quantity={Quantity}",
            userId, productId, quantity);

        return cart;
    }

    public async Task<Cart> UpdateItemAsync(Guid userId, Guid cartItemId, int quantity, CancellationToken ct = default)
    {
        if (quantity is < 1 or > 999)
            throw new ValidationException(
                "Quantity must be between 1 and 999.", "QUANTITY_LIMIT_EXCEEDED");
        
        var cart = await dbContext.Carts
                       .Include(c => c.Items)
                       .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                   ?? throw new NotFoundException("Cart not found.", "CART_NOT_FOUND");

        // Ownership is implicit: the item must be in *this* user's cart.
        var cartItem = cart.Items.FirstOrDefault(i => i.Id == cartItemId)
                       ?? throw new NotFoundException("Cart item not found.", "CART_ITEM_NOT_FOUND");

        // Stock check uses the *new* quantity directly (replace, not increment).
        var product = await dbContext.Products
                          .AsNoTracking()
                          .Include(p => p.Images.Where(img => img.IsPrimary))
                          .FirstOrDefaultAsync(p => p.Id == cartItem.ProductId && !p.IsDeleted, ct)
                      ?? throw new NotFoundException("Product not found.", "PRODUCT_NOT_FOUND");

        if (product.StockQuantity < quantity)
            throw new ConflictException(
                $"Insufficient stock. Available: {product.StockQuantity}.",
                "INSUFFICIENT_STOCK");

        // Mutate via domain (sets quantity + refreshes price snapshot).
        cart.UpdateItem(cartItemId, quantity, product.Price);

        await cartValidator.ValidateAndThrowAsync(cart, ct);
        await dbContext.SaveChangesAsync(ct);
        await LoadProductNavigationsAsync(cart, ct);
        
        logger.LogInformation(
            "Cart item updated. UserId={UserId} CartItemId={CartItemId} Quantity={Quantity}",
            userId, cartItemId, quantity);

        return cart;
    }

    public async Task RemoveItemAsync(Guid userId, Guid cartItemId, CancellationToken ct = default)
    {
        var cart = await dbContext.Carts
                       .Include(c => c.Items)
                       .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                   ?? throw new NotFoundException("Cart not found.", "CART_NOT_FOUND");

        // Verify the item belongs to this user's cart before delegating to domain.
        if (cart.Items.All(i => i.Id != cartItemId))
            throw new NotFoundException("Cart item not found.", "CART_ITEM_NOT_FOUND");

        cart.RemoveItem(cartItemId);
        await dbContext.SaveChangesAsync(ct);
        
        logger.LogInformation(
            "Cart item removed. UserId={UserId} CartItemId={CartItemId}",
            userId, cartItemId);
    }

    public async Task ClearCartAsync(Guid userId, CancellationToken ct = default)
    {
        var cart = await dbContext.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, ct);

        // Idempotent — if no cart exists the user's cart is already empty.
        if (cart is null) return;

        cart.Clear();
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Cart cleared. UserId={UserId}", userId);
    }
    
    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Ensures Product navigation is loaded on every CartItem so ToResponse()
    /// can read ProductName without a separate query. Mirrors the User nav-load
    /// pattern in RatingService.
    /// </summary>
    private async Task LoadProductNavigationsAsync(Cart cart, CancellationToken ct)
    {
        foreach (var item in cart.Items)
        {
            if (!dbContext.Entry(item).Reference(i => i.Product).IsLoaded)
                await dbContext.Entry(item).Reference(i => i.Product).LoadAsync(ct);

            // Load only the primary image
            if (!dbContext.Entry(item.Product).Collection(p => p.Images).IsLoaded)
                await dbContext.Entry(item.Product)
                    .Collection(p => p.Images)
                    .Query()
                    .Where(img => img.IsPrimary)
                    .LoadAsync(ct);
        }
    }
}