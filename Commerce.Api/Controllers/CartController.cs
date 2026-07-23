using System.Security.Claims;
using Asp.Versioning;
using Commerce.Api.Mappings;
using Commerce.Application.Services.Carts;
using Commerce.Contracts.Carts;
using Commerce.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Cart.GetCart)]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var cart = await cartService.GetOrCreateCartAsync(GetUserId(), ct);
        return Ok(cart.ToResponse());
    }

    [HttpPost(ApiEndpoints.Cart.PostCartItem)]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddItem(
        [FromBody] AddCartItemRequest request,
        CancellationToken ct)
    {
        var cart = await cartService.AddItemAsync(
            GetUserId(), request.ProductId, request.Quantity, ct);

        return Ok(cart.ToResponse());
    }

    [HttpPut(ApiEndpoints.Cart.PutCartItem)]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItem(
        Guid id,
        [FromBody] UpdateCartItemRequest request,
        CancellationToken ct)
    {
        var cart = await cartService.UpdateItemAsync(GetUserId(), id, request.Quantity, ct);
        return Ok(cart.ToResponse());
    }

    [HttpDelete(ApiEndpoints.Cart.DeleteCartItem)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(Guid id, CancellationToken ct)
    {
        await cartService.RemoveItemAsync(GetUserId(), id, ct);
        return NoContent();
    }

    [HttpDelete(ApiEndpoints.Cart.DeleteCart)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await cartService.ClearCartAsync(GetUserId(), ct);
        return NoContent();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}