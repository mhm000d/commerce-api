using Asp.Versioning;
using Commerce.Api.Mappings;
using Commerce.Application.Models;
using Commerce.Application.Services.Admin;
using Commerce.Contracts.Orders;
using Commerce.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize(Roles = nameof(UserRole.Admin))]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Admin.GetOrder)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct)
    {
        var order = await adminService.GetOrderByIdAsync(id, ct);
        return Ok(order.ToResponse());
    }
    
    [HttpGet(ApiEndpoints.Admin.GetOrders)]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct     = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (orders, total) = await adminService.GetAllOrdersAsync(page, pageSize, ct);
        return Ok(orders.ToPagedResponse(page, pageSize, total));
    }
    
    [HttpPut(ApiEndpoints.Admin.UpdateOrderStatus)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        // Parse the string status here in the controller so invalid values become
        // a clean 400 rather than bubbling up as a ValidationException from the service.
        if (!Enum.TryParse<OrderStatus>(request.NewStatus, ignoreCase: true, out var status))
        {
            return BadRequest(new
            {
                error = $"'{request.NewStatus}' is not a valid order status.",
                code  = "INVALID_STATUS",
            });
        }

        var order = await adminService.UpdateOrderStatusAsync(id, status, ct);
        return Ok(order.ToResponse());
    }
}
