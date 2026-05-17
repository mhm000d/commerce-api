using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace Commerce.Application.Services.Payments;

public class StripeService(IConfiguration configuration) : IStripeService
{
    private readonly string _webhookSecret =
        configuration["Stripe:WebhookSecret"]
        ?? throw new InvalidOperationException("Stripe:WebhookSecret is not configured.");

    private readonly string _frontendBaseUrl =
        configuration["Frontend:BaseUrl"]
        ?? throw new InvalidOperationException("Frontend:BaseUrl is not configured.");

    public async Task<(string SessionId, string ClientSecret)> CreateCheckoutSessionAsync(Guid orderId,
        string orderNumber,
        string customerEmail,
        IEnumerable<CheckoutLineItem> lineItems,
        string returnUrl,
        CancellationToken ct = default)
    {
        var options = new SessionCreateOptions
        {
            UiMode = "embedded",
            Mode = "payment",
            ClientReferenceId = orderId.ToString(),
            CustomerEmail = customerEmail,
            ReturnUrl = returnUrl,
            LineItems = lineItems.Select(li => new SessionLineItemOptions
            {
                Quantity = li.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "usd",
                    UnitAmount = (long)(li.UnitPrice * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = li.ProductName,
                        Images = li.PrimaryImageUrl is not null
                            ? [li.PrimaryImageUrl]
                            : null,
                    },
                },
            }).ToList(),
            Metadata = new Dictionary<string, string>
            {
                ["order_number"] = orderNumber,
                ["order_id"] = orderId.ToString(),
            },
        };

        var service = new SessionService();
        var session = await service.CreateAsync(options, cancellationToken: ct);
        return (session.Id, session.ClientSecret);
    }

    public async Task RefundAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var options = new RefundCreateOptions { PaymentIntent = paymentIntentId };
        var service = new RefundService();
        await service.CreateAsync(options, cancellationToken: ct);
    }

    public async Task<StripeSessionStatus> GetSessionStatusAsync(string sessionId, CancellationToken ct = default)
    {
        var service = new SessionService();
        var session = await service.GetAsync(sessionId, cancellationToken: ct);
        return new StripeSessionStatus(session.Status, session.CustomerDetails?.Email);
    }

    public bool TryParseWebhookEvent(
        string payload, string signature, out ParsedStripeEvent? stripeEvent)
    {
        try
        {
            var evt = EventUtility.ConstructEvent(payload, signature, _webhookSecret);
            var session = evt.Data.Object as Session;

            stripeEvent = new ParsedStripeEvent(
                Id: evt.Id,
                Type: evt.Type,
                ClientReferenceId: session?.ClientReferenceId,
                PaymentIntentId: session?.PaymentIntentId,
                AmountTotal: session?.AmountTotal is { } amt ? amt / 100m : null);
            return true;
        }
        catch
        {
            stripeEvent = null;
            return false;
        }
    }
}