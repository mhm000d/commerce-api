using Commerce.Api.Mappings;
using Commerce.Application.Models;
using Commerce.Application.Services.Admin;
using Commerce.Contracts.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
public class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet(ApiEndpoints.Admin.GetOrders)]
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
