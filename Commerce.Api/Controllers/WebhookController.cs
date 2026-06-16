using Commerce.Application.Database;
using Commerce.Application.Models;
using Commerce.Application.Services.Email;
using Commerce.Application.Services.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Controllers;

[ApiController]
public class WebhookController(
    AppDbContext dbContext,
    IStripeService stripeService,
    IEmailNotificationService emailService,
    ILogger<WebhookController> logger) : ControllerBase
{
    // Stripe requires the raw request body for signature verification —
    // do not let ASP.NET Core's model binding touch it first.
    [HttpPost(ApiEndpoints.Webhooks.Stripe)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StripeWebhook(CancellationToken ct)
    {
        var payload = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        // 1. Verify signature — reject anything that doesn't match the webhook secret.
        if (!stripeService.TryParseWebhookEvent(payload, signature, out var stripeEvent)
            || stripeEvent is null)
        {
            logger.LogWarning("Invalid Stripe webhook signature received.");
            return BadRequest();
        }

        // 2. Idempotency guard — Stripe may deliver the same event more than once.
        var alreadyProcessed = await dbContext.WebhookEvents
            .AnyAsync(e => e.EventId == stripeEvent.Id, ct);

        if (alreadyProcessed)
            return Ok(); // acknowledge without reprocessing

        // 3. Persist the raw event immediately so we have an audit trail even if
        //    processing fails, and so a retry is caught by the guard above.
        var webhookEvent = WebhookEvent.Create(stripeEvent.Id, stripeEvent.Type, payload);
        dbContext.WebhookEvents.Add(webhookEvent);
        await dbContext.SaveChangesAsync(ct);

        // 4. Process — errors are caught so we always return 200 to Stripe.
        //    Stripe stops retrying on 4xx/5xx, so the safe contract is: save the
        //    raw event first (step 3), then process best-effort.
        try
        {
            await ProcessEventAsync(stripeEvent, ct);
            webhookEvent.MarkProcessed();
        }
        catch (Exception ex)
        {
            webhookEvent.MarkFailed(ex.Message);
            logger.LogError(ex,
                "Webhook processing failed. EventId={EventId} Type={Type}",
                stripeEvent.Id, stripeEvent.Type);
        }

        await dbContext.SaveChangesAsync(ct);
        return Ok();
    }
    
    // ── Event Handlers ────────────────────────────────────────────────────────
    private async Task ProcessEventAsync(ParsedStripeEvent e, CancellationToken ct)
    {
        switch (e.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompletedAsync(e, ct);
                break;

            case "checkout.session.expired":
                await HandleCheckoutExpiredAsync(e, ct);
                break;

            default:
                logger.LogInformation(
                    "Unhandled Stripe event type: {Type}", e.Type);
                break;
        }
    }
    
    private async Task HandleCheckoutCompletedAsync(ParsedStripeEvent e, CancellationToken ct)
    {
        // ClientReferenceId was set to orderId.ToString() at session creation.
        if (!Guid.TryParse(e.ClientReferenceId, out var orderId))
            throw new InvalidOperationException(
                $"Invalid ClientReferenceId '{e.ClientReferenceId}' in checkout.session.completed.");

        var payment = await LoadPaymentWithOrderAsync(orderId, ct);

        // Swap the Checkout SessionId for the PaymentIntentId — we need the
        // PaymentIntentId if we ever initiate a refund via Stripe's API.
        if (!string.IsNullOrEmpty(e.PaymentIntentId))
            payment.UpdateProviderId(e.PaymentIntentId);

        payment.MarkCompleted();
        payment.Order.MarkAsPaid();

        // Queue confirmation email — EmailSenderJob picks it up within 1 minute.
        var user = await dbContext.Users.FindAsync([payment.Order.UserId], ct)
                   ?? throw new InvalidOperationException("User not found for order.");

        // var notification = EmailNotification.Create(
        //     recipientEmail: user.Email,
        //     template:       EmailTemplate.OrderConfirmation,
        //     templateData: new Dictionary<string, string>
        //     {
        //         ["CustomerName"] = user.Name,
        //         ["OrderNumber"] = payment.Order.OrderNumber,
        //         ["OrderId"]      = payment.OrderId.ToString(),
        //         ["TotalAmount"] = payment.Order.TotalAmount.ToString("F2"),
        //         ["Items"]        = System.Text.Json.JsonSerializer.Serialize(lineItems)
        //     },
        //     orderId: payment.OrderId);
        //
        // dbContext.EmailNotifications.Add(notification);

        var lineItems = payment.Order.Items.Select(i => new OrderLineItemData(
            ProductName: i.Product!.Name,
            ImageUrl:    i.Product.Images.FirstOrDefault(/*img => img.IsPrimary*/)?.ImageUrl,
            UnitPrice:   i.UnitPrice,
            Quantity:    i.Quantity));

        await emailService.QueueOrderConfirmationAsync(
            user.Email,
            user.Name,
            payment.Order.OrderNumber,
            payment.Order.Id.ToString(),
            payment.Order.TotalAmount,
            lineItems,
            ct,
            paymentMethod: "Card",
            paymentStatus: "Paid");
        
        logger.LogInformation(
            "Checkout completed. OrderId={OrderId} PaymentIntentId={IntentId}",
            orderId, e.PaymentIntentId);
    }
    
    private async Task HandleCheckoutExpiredAsync(ParsedStripeEvent e, CancellationToken ct)
    {
        // Session expired without payment — treat identically to payment failure.
        // PaymentTimeoutJob will also catch this, but the webhook is faster.
        if (!Guid.TryParse(e.ClientReferenceId, out var orderId))
            return;
        
        var payment = await LoadPaymentWithOrderAsync(orderId, ct);

        if (payment.Order.Status == OrderStatus.Cancelled)
            return; // already handled

        payment.MarkFailed();

        foreach (var item in payment.Order.Items)
            item.Product!.RestoreStock(item.Quantity);

        payment.Order.Cancel(isAdmin: true);

        logger.LogWarning(
            "Checkout session expired. OrderId={OrderId}", orderId);
    }
    
    private async Task<Payment> LoadPaymentWithOrderAsync(Guid orderId, CancellationToken ct)
    {
        // Look up by OrderId — we set ClientReferenceId = orderId, so this is
        // our stable reconciliation key regardless of which Stripe ID was stored.
        return await dbContext.Payments
                   .Include(p => p.Order)
                   .ThenInclude(o => o.Items)
                   .ThenInclude(i => i.Product)
                   .ThenInclude(p => p.Images.Where(img => img.IsPrimary))
                   .FirstOrDefaultAsync(p => p.OrderId == orderId, ct)
               ?? throw new InvalidOperationException(
                   $"No payment found for OrderId '{orderId}'.");
    }
}
