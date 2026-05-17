using Commerce.Application.Database;
using Commerce.Application.Models;
using Commerce.Application.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Commerce.Application.Jobs;

/// <summary>
/// Hangfire recurring job — runs every 5 minutes.
/// Finds card payments that have been PENDING for more than 30 minutes
/// and cancels the associated order + restores stock.
///
/// COD payments are intentionally excluded — they stay PENDING until
/// an admin manually marks them PAID after physical collection.
///
/// Idempotent: the Status = PENDING guard ensures a payment that was
/// already timed out by a previous run (or by the webhook) is skipped.
/// </summary>
public class PaymentTimeoutJob(
    AppDbContext dbContext,
    IStripeService stripeService,
    ILogger<PaymentTimeoutJob> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - Timeout;

        var timedOutPayments = await dbContext.Payments
            .Include(p => p.Order)
            .ThenInclude(o => o.Items)
            .ThenInclude(i => i.Product)
            .Where(p => p.Status == PaymentStatus.Pending
                        && p.PaymentMethod == "card" // never timeout COD
                        && p.CreatedAt < cutoff
                        && p.Order.Status != OrderStatus.Cancelled) // skip already handled
            .ToListAsync(ct);

        if (timedOutPayments.Count == 0)
            return;

        logger.LogInformation(
            "PaymentTimeoutJob: found {Count} timed-out payment(s)", timedOutPayments.Count);

        foreach (var payment in timedOutPayments)
            await TimeoutPaymentAsync(payment, ct);

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("PaymentTimeoutJob: batch complete");
    }

    private async Task TimeoutPaymentAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            payment.MarkFailed();

            // Restore stock for every item in the order.
            foreach (var item in payment.Order.Items)
                item.Product!.RestoreStock(item.Quantity);

            // isAdmin: true — system-initiated cancel is not bound by user rules.
            payment.Order.Cancel(isAdmin: true);

            // If somehow the payment was completed (race between webhook + job),
            // initiate a refund. In practice this guard is rarely hit because the
            // webhook fires much faster than the 30-minute window.
            if (payment.Status == PaymentStatus.Completed)
            {
                await stripeService.RefundAsync(payment.PaymentProviderId, ct);
                payment.MarkRefunded();

                logger.LogWarning(
                    "PaymentTimeoutJob: refund initiated for already-completed payment. " +
                    "OrderId={OrderId} PaymentId={PaymentId}",
                    payment.OrderId, payment.Id);
            }

            logger.LogWarning(
                "PaymentTimeoutJob: order timed out. " +
                "OrderId={OrderId} PaymentId={PaymentId} CreatedAt={CreatedAt}",
                payment.OrderId, payment.Id, payment.CreatedAt);
        }
        catch (Exception ex)
        {
            // Log and continue — a single bad record must not abort the whole batch.
            logger.LogError(ex,
                "PaymentTimeoutJob: failed to process payment. " +
                "PaymentId={PaymentId} OrderId={OrderId}",
                payment.Id, payment.OrderId);
        }
    }
}