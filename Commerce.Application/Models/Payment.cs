namespace Commerce.Application.Models;

public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }

    /// <summary>Stripe charge_id — used for idempotency and refunds.</summary>
    public string PaymentProviderId { get; private set; } = null!;

    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string PaymentMethod { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    
    // ── Navigation Properties ─────────────────────────────────────────────────
    public Order Order { get; private set; } = null!;
    
    // ── Factory ───────────────────────────────────────────────────────────────
    public static Payment Create(Guid orderId, string paymentProviderId, decimal amount, string paymentMethod)
    {
        return new Payment
        {
            Id                = Guid.NewGuid(),
            OrderId           = orderId,
            PaymentProviderId = paymentProviderId,
            Amount            = amount,
            Status            = PaymentStatus.Pending,
            PaymentMethod     = paymentMethod,
            CreatedAt         = DateTimeOffset.UtcNow,
        };
    }
    
    // ── Behaviour ─────────────────────────────────────────────────────────────
    public void MarkPending()   => Status = PaymentStatus.Pending;
    public void MarkCompleted() => Status = PaymentStatus.Completed;
    public void MarkFailed()    => Status = PaymentStatus.Failed;
    public void MarkRefunded()  => Status = PaymentStatus.Refunded;
    public void UpdateProviderId(string newProviderId) =>
        PaymentProviderId = newProviderId;
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}

public enum CheckoutPaymentMethod { Card, CashOnDelivery }