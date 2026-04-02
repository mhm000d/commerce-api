using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class WebhookEventValidator : AbstractValidator<WebhookEvent>
{
    public WebhookEventValidator()
    {
        RuleFor(x => x.EventId)
            .NotEmpty()
            .MaximumLength(255)
            .Matches(@"^evt_[a-zA-Z0-9]+$")
            .WithMessage("Invalid Stripe event ID format.");

        RuleFor(x => x.EventType)
            .NotEmpty()
            .MaximumLength(255);

        // Optional: restrict to known Stripe events (recommended)
        RuleFor(x => x.EventType)
            .Must(BeValidEventType)
            .WithMessage("Unsupported webhook event type.");

        RuleFor(x => x.Payload)
            .NotEmpty()
            .Must(BeValidJson)
            .WithMessage("Payload must be valid JSON.");

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.ProcessedAt)
            .NotNull()
            .When(x => x.Status == WebhookStatus.Processed)
            .WithMessage("ProcessedAt is required when status is Processed.");

        RuleFor(x => x.ErrorMessage)
            .NotEmpty()
            .MaximumLength(1000)
            .When(x => x.Status == WebhookStatus.Failed)
            .WithMessage("ErrorMessage is required when status is Failed.");

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow);
    }

    // ── Helpers ────────────────────────────────────────────
    private bool BeValidJson(string payload)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(payload);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool BeValidEventType(string eventType) => eventType switch
    {
        "checkout.session.completed" => true,
        "checkout.session.expired"   => true,
        _ => false
    };
}