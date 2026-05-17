using Commerce.Application.Models;

namespace Commerce.Application.Services.Admin;

public interface IAdminService
{
    Task<(IEnumerable<Order> Orders, int TotalCount)> GetAllOrdersAsync(
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Advances the order through the state machine, or cancels it.
    /// Restores stock and initiates a refund automatically when cancelling.
    /// </summary>
    Task<Order> UpdateOrderStatusAsync(
        Guid orderId, OrderStatus newStatus, CancellationToken ct = default);
}