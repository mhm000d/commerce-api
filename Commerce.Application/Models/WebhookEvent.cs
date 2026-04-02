namespace Commerce.Application.Models;

public class WebhookEvent
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Stripe event ID (e.g. "evt_3PxQ...").
    /// Unique index guarantees idempotent processing.
    /// </summary>
    public string EventId { get; private set; } = null!;
    public string EventType { get; private set; } = null!;

    /// <summary>
    /// Raw Stripe payload stored as jsonb string for auditability and potential replay.
    /// </summary>
    public string Payload { get; private set; } = null!;

    public WebhookStatus Status { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    
    // ── Factory ───────────────────────────────────────────────────────────────
    public static WebhookEvent Create(string eventId, string eventType, string payload)
    {
        return new WebhookEvent
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = eventType,
            Payload = payload,
            Status = WebhookStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    public void MarkProcessed()
    {
        Status = WebhookStatus.Processed;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = WebhookStatus.Failed;
        ErrorMessage = errorMessage;
    }
}

public enum WebhookStatus
{
    Pending,
    Processed,
    Failed
}