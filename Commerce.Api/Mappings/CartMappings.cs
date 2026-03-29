using Commerce.Application.Models;
using Commerce.Contracts.Carts;

namespace Commerce.Api.Mappings;

public static class CartMappings
{
    public static CartItemResponse ToResponse(this CartItem item) => new(
        Id: item.Id,
        ProductId: item.ProductId,
        ProductName: item.Product.Name,
        PrimaryImageUrl: item.Product.Images.FirstOrDefault()?.ImageUrl,
        Quantity: item.Quantity,
        UnitPriceSnapshot: item.UnitPriceSnapshot);

    public static CartResponse ToResponse(this Cart cart) => new(
        Id: cart.Id,
        UpdatedAt: cart.UpdatedAt,
        Items: cart.Items.Select(i => i.ToResponse()).ToList(),
        Subtotal: cart.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity));
}