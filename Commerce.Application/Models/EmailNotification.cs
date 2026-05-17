namespace Commerce.Application.Models;

public class EmailNotification
{
    public Guid Id { get; private set; }
    public string RecipientEmail { get; private set; } = null!;
    public EmailTemplate Template { get; private set; }

    /// <summary>
    /// Dynamic template variables stored as JSON (jsonb).
    /// Mapped with HasConversion + HasColumnType("jsonb") —
    /// Dictionary is a known type, no dynamic serialization issue.
    /// </summary>
    public Dictionary<string, string> TemplateData { get; private set; } = [];

    public EmailStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? OrderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // ── Navigation Properties ─────────────────────────────────────────────────
    public Order? Order { get; private set; }

    // ── Factory ───────────────────────────────────────────────────────────────
    public static EmailNotification Create(
        string recipientEmail,
        EmailTemplate template,
        Dictionary<string, string> templateData,
        Guid? orderId = null,
        int maxAttempts = 3)
    {
        return new EmailNotification
        {
            Id = Guid.NewGuid(),
            RecipientEmail = recipientEmail,
            Template = template,
            TemplateData = templateData,
            Status = EmailStatus.Pending,
            Attempts = 0,
            MaxAttempts = maxAttempts,
            OrderId = orderId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    // ── Behaviour ─────────────────────────────────────────────────────────────
    /// <summary>Called by EmailSenderJob on each attempt.</summary>
    public void RecordAttempt(bool success, string? errorMessage = null)
    {
        Attempts++;
        LastAttemptAt = DateTimeOffset.UtcNow;

        if (success)
        {
            Status = EmailStatus.Sent;
            SentAt = DateTimeOffset.UtcNow;
        }
        else
        {
            ErrorMessage = errorMessage;
            Status = Attempts >= MaxAttempts
                ? EmailStatus.PermanentlyFailed
                : EmailStatus.Failed;
        }
    }
    
    /// <summary>
    /// Forces Attempts to MaxAttempts so the next RecordAttempt(false)
    /// immediately sets Status = PermanentlyFailed.
    /// Only called for non-retriable delivery failures.
    /// </summary>
    public void ForceExhaustAttempts() => Attempts = MaxAttempts - 1;
}

public enum EmailTemplate
{
    OrderConfirmation,
    PasswordReset
}

public enum EmailStatus
{
    Pending,
    Sent,
    Failed,
    PermanentlyFailed
}