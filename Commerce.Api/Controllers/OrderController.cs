using System.Security.Claims;
using Commerce.Api.Mappings;
using Commerce.Application.Models;
using Commerce.Application.Services.Orders;
using Commerce.Contracts.Orders;
using Commerce.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class OrderController(IOrderService orderService) : ControllerBase
{
    [HttpPost(ApiEndpoints.Orders.Checkout)]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken ct)
    {
        // Console.WriteLine($"[DEBUG_LOG] Checkout starting. User: {User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")}");
        // Parse payment method here — keeps the service layer free of string parsing.
        if (!Enum.TryParse<CheckoutPaymentMethod>(
                request.PaymentMethod, ignoreCase: true, out var paymentMethod))
        {
            return BadRequest(new
            {
                error = $"'{request.PaymentMethod}' is not a valid payment method.",
                code = "INVALID_PAYMENT_METHOD",
            });
        }

        var (order, stripeClientSecret) = await orderService.CheckoutAsync(
            GetUserId(), request.AddressId, paymentMethod, ct);

        var response = order.ToCheckoutResponse(stripeClientSecret);

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, response);
    }

    [HttpGet(ApiEndpoints.Orders.GetCheckoutSessionStatus)]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<CheckoutSessionStatusResponse> GetSessionStatus(
        [FromQuery] string sessionId,
        CancellationToken ct)
    {
        var (status, customerEmail, orderId) = await orderService.GetCheckoutSessionStatusAsync(sessionId, ct);
        return new CheckoutSessionStatusResponse(status, customerEmail, orderId);
    }

    [HttpGet(ApiEndpoints.Orders.GetOrder)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct)
    {
        var order = await orderService.GetOrderAsync(GetUserId(), id, ct);
        return Ok(order.ToResponse());
    }

    [HttpGet(ApiEndpoints.Orders.GetOrders)]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (orders, total) = await orderService.GetOrdersAsync(
            GetUserId(), page, pageSize, ct);

        return Ok(orders.ToPagedResponse(page, pageSize, total));
    }

    [HttpPost(ApiEndpoints.Orders.CancelOrder)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var order = await orderService.CancelOrderAsync(GetUserId(), id, ct);
        return Ok(order.ToResponse());
    }

    [HttpPost(ApiEndpoints.Orders.RetryPayment)]
    [ProducesResponseType(typeof(CheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryPayment(Guid id, CancellationToken ct)
    {
        var (clientSecret, sessionId) = await orderService.RetryPaymentAsync(GetUserId(), id, ct);
        return Ok(new { clientSecret, orderId = id });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}