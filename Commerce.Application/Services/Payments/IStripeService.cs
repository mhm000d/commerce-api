namespace Commerce.Application.Services.Payments;

public interface IStripeService
{
    /// <summary>
    /// Creates a Stripe hosted Checkout Session.
    /// Returns the session ID (stored on Payment.PaymentProviderId) and the
    /// hosted URL the frontend should redirect the customer to.
    /// 
    /// ClientReferenceId is set to orderId.ToString() so the webhook can
    /// reconcile the session back to our Order without a database lookup by
    /// Stripe-internal IDs.
    /// </summary>
    Task<(string SessionId, string ClientSecret)> CreateCheckoutSessionAsync(
        Guid                                      orderId,
        string                                    orderNumber,
        string                                    customerEmail,
        IEnumerable<CheckoutLineItem>             lineItems,
        string                                    returnUrl,
        CancellationToken                         ct = default);
    
    /// <summary>Initiates a full refund for the given PaymentIntent.</summary>
    Task RefundAsync(string paymentIntentId, CancellationToken ct = default);

    /// <summary>Gets the status and customer email of a Stripe Checkout Session.</summary>
    Task<StripeSessionStatus> GetSessionStatusAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Verifies the Stripe-Signature header and parses the raw payload into a
    /// <see cref="ParsedStripeEvent"/>.  Returns false if the signature is invalid.
    /// </summary>
    bool TryParseWebhookEvent(
        string            payload,
        string            signature,
        out ParsedStripeEvent? stripeEvent);
}

/// <summary>
/// Slim line-item descriptor passed from the service layer to avoid coupling
/// the application to Stripe SDK types.
/// </summary>
public record CheckoutLineItem(
    string  ProductName,
    string? PrimaryImageUrl,
    decimal UnitPrice,
    int     Quantity);

/// <summary>
/// Slim representation of a Stripe event.  Shape now matches the Checkout Session
/// webhook rather than the PaymentIntent webhook.
/// </summary>
public record ParsedStripeEvent(
    string  Id,                  // evt_xxx
    string  Type,                // "checkout.session.completed" | "checkout.session.expired" | …
    string? ClientReferenceId,   // our orderId — set at session creation
    string? PaymentIntentId,     // Stripe's PaymentIntentId — needed for refunds
    decimal? AmountTotal);       // session.AmountTotal / 100

/// <summary>
/// Status and customer email of a Stripe Checkout Session.
/// </summary>
public record StripeSessionStatus(
    string  Status,
    string? CustomerEmail);