using Commerce.Application.Database;
using Commerce.Application.Exceptions;
using Commerce.Application.Models;
using Commerce.Application.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Services.Admin;

public class AdminService(
    AppDbContext dbContext,
    IStripeService stripeService,
    ILogger<AdminService> logger) : IAdminService
{
    public async Task<(IEnumerable<Order> Orders, int TotalCount)> GetAllOrdersAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext.Orders
            .OrderByDescending(o => o.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Items)
            .Include(o => o.Payment)
            .ToListAsync(ct);

        return (orders, totalCount);
    }

    public async Task<Order> UpdateOrderStatusAsync(Guid orderId, OrderStatus newStatus, CancellationToken ct = default)
    {
        var order = await dbContext.Orders
                        .Include(o => o.Items)
                        .ThenInclude(i => i.Product)
                        .Include(o => o.Payment)
                        .FirstOrDefaultAsync(o => o.Id == orderId, ct)
                    ?? throw new NotFoundException("Order not found.", "ORDER_NOT_FOUND");

        try
        {
            switch (newStatus)
            {
                // Admins can manually mark an order paid (e.g., offline payment or testing).
                case OrderStatus.Paid:
                    order.MarkAsPaid();
                    break;

                case OrderStatus.Shipped:
                    order.MarkAsShipped();
                    break;

                case OrderStatus.Delivered:
                    order.MarkAsDelivered();
                    break;

                case OrderStatus.Cancelled:
                    order.Cancel(isAdmin: true);
                    await RestoreStockAndRefundAsync(order, ct);
                    break;

                // PLACED is the initial state — it can't be set manually.
                default:
                    throw new ValidationException(
                        $"Status '{newStatus}' cannot be set via this endpoint.",
                        "INVALID_STATUS");
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message, "INVALID_ORDER_TRANSITION");
        }
        
        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Admin updated order status. OrderId={OrderId} NewStatus={NewStatus}",
            orderId, newStatus);

        return order;
    }
    
    // ── Private Helpers ───────────────────────────────────────────────────────
    private async Task RestoreStockAndRefundAsync(Order order, CancellationToken ct)
    {
        foreach (var item in order.Items)
            item.Product!.RestoreStock(item.Quantity);

        if (order.Payment?.Status == PaymentStatus.Completed)
        {
            await stripeService.RefundAsync(order.Payment.PaymentProviderId, ct);
            order.Payment.MarkRefunded();

            logger.LogInformation(
                "Refund initiated. OrderId={OrderId} PaymentId={PaymentId}",
                order.Id, order.Payment.Id);
        }
    }
}