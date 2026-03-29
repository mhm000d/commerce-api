using System.Security.Claims;
using Commerce.Api.Mappings;
using Commerce.Application.Services.Carts;
using Commerce.Contracts.Carts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Authorize]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Cart.GetCart)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var cart = await cartService.GetOrCreateCartAsync(GetUserId(), ct);
        return Ok(cart.ToResponse());
    }

    [HttpPost(ApiEndpoints.Cart.PostCartItem)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken ct)
    {
        var cart = await cartService.AddItemAsync(
            GetUserId(), request.ProductId, request.Quantity, ct);

        return Ok(cart.ToResponse());
    }

    [HttpPut(ApiEndpoints.Cart.PutCartItem)]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken ct)
    {
        var cart = await cartService.UpdateItemAsync(GetUserId(), id, request.Quantity, ct);
        return Ok(cart.ToResponse());
    }

    [HttpDelete(ApiEndpoints.Cart.DeleteCartItem)]
    public async Task<IActionResult> RemoveItem(Guid id, CancellationToken ct)
    {
        await cartService.RemoveItemAsync(GetUserId(), id, ct);
        return NoContent();
    }

    [HttpDelete(ApiEndpoints.Cart.DeleteCart)]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await cartService.ClearCartAsync(GetUserId(), ct);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}