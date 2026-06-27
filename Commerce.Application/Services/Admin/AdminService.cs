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
    public async Task<Order> GetOrderByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        return await dbContext.Orders
                   .Include(o => o.Items)
                   .ThenInclude(i => i.Product)
                   .ThenInclude(p => p.Images.Where(img => img.IsPrimary))
                   .Include(o => o.Payment)
                   .FirstOrDefaultAsync(o => o.Id == orderId, ct)
               ?? throw new NotFoundException("Order not found.", "ORDER_NOT_FOUND");
    }

    public async Task<(IEnumerable<Order> Orders, int TotalCount)> GetAllOrdersAsync(int page, int pageSize,
        CancellationToken ct = default)
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

        var isCOD = order.Payment?.PaymentMethod?.Equals("cash_on_delivery", StringComparison.OrdinalIgnoreCase) == true;

        logger.LogInformation("Updating order status. OrderId={OrderId} CurrentStatus={CurrentStatus} NewStatus={NewStatus} IsCOD={IsCOD}",
            orderId, order.Status, newStatus, isCOD);

        try
        {
            if (isCOD)
            {
                // Define allowed transitions for COD
                bool allowed = (order.Status, newStatus) switch
                {
                    // Placed → Shipped (skip Paid)
                    (OrderStatus.Placed, OrderStatus.Shipped) => true,
                    // Shipped → Delivered
                    (OrderStatus.Shipped, OrderStatus.Delivered) => true,
                    // Delivered → Paid (payment collected on delivery)
                    (OrderStatus.Delivered, OrderStatus.Paid) => true,
                    // Any status → Cancelled (admin cancel)
                    (_, OrderStatus.Cancelled) => true,
                    // Allow other forward transitions (e.g., Placed → Delivered directly? Not typical, but we allow all forward)
                    (_, _) => true
                };

                if (!allowed)
                    throw new ConflictException($"Invalid transition for COD order: {order.Status} → {newStatus}", "INVALID_ORDER_TRANSITION");

                // Use AdminSetStatus to bypass the state machine
                order.AdminSetStatus(newStatus);

                // If cancelling, restore stock and initiate refund (if needed)
                if (newStatus == OrderStatus.Cancelled)
                    await RestoreStockAndRefundAsync(order, ct);
            }
            else
            {
                // Card payments: use normal state machine
                switch (newStatus)
                {
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
                    default:
                        throw new ValidationException($"Status '{newStatus}' cannot be set.", "INVALID_STATUS");
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new ConflictException(ex.Message, "INVALID_ORDER_TRANSITION");
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Admin updated order status. OrderId={OrderId} NewStatus={NewStatus}", orderId, newStatus);

        await LoadOrderNavigationsAsync(order, ct);
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

    /// <summary>
    /// Loads navigation properties of an order that are not automatically loaded,
    /// so the returned order object is complete when sent to the frontend.
    /// </summary>
    private async Task LoadOrderNavigationsAsync(Order order, CancellationToken ct)
    {
        if (!dbContext.Entry(order).Collection(o => o.Items).IsLoaded)
            await dbContext.Entry(order).Collection(o => o.Items).LoadAsync(ct);

        foreach (var item in order.Items)
        {
            if (!dbContext.Entry(item).Reference(i => i.Product).IsLoaded)
                await dbContext.Entry(item).Reference(i => i.Product).LoadAsync(ct);

            // Load only the primary image for each product
            if (item.Product is not null && !dbContext.Entry(item.Product).Collection(p => p.Images).IsLoaded)
                await dbContext.Entry(item.Product)
                    .Collection(p => p.Images)
                    .Query()
                    .Where(img => img.IsPrimary)
                    .LoadAsync(ct);
        }

        if (!dbContext.Entry(order).Reference(o => o.Payment).IsLoaded)
            await dbContext.Entry(order).Reference(o => o.Payment).LoadAsync(ct);
    }
}