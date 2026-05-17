using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class EmailNotificationValidator : AbstractValidator<EmailNotification>
{
    public EmailNotificationValidator()
    {
        RuleFor(x => x.RecipientEmail)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(255);

        RuleFor(x => x.Template)
            .IsInEnum();

        RuleFor(x => x.TemplateData)
            .NotNull()
            .Must(d => d.Count > 0)
            .WithMessage("Template data must not be empty.");

        // Optional: prevent empty keys/values
        RuleForEach(x => x.TemplateData)
            .Must(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .WithMessage("Template data keys must not be empty.");

        RuleForEach(x => x.TemplateData)
            .Must(kv => kv.Value != null)
            .WithMessage("Template data values must not be null.");

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.Attempts)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.MaxAttempts)
            .GreaterThan(0);

        RuleFor(x => x)
            .Must(x => x.Attempts <= x.MaxAttempts)
            .WithMessage("Attempts cannot exceed MaxAttempts.");

        RuleFor(x => x.SentAt)
            .NotNull()
            .When(x => x.Status == EmailStatus.Sent)
            .WithMessage("SentAt is required when email is sent.");

        RuleFor(x => x.ErrorMessage)
            .NotEmpty()
            .MaximumLength(1000)
            .When(x => x.Status is EmailStatus.Failed or EmailStatus.PermanentlyFailed)
            .WithMessage("ErrorMessage is required when email fails.");

        RuleFor(x => x.LastAttemptAt)
            .NotNull()
            .When(x => x.Attempts > 0)
            .WithMessage("LastAttemptAt must be set after first attempt.");

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow);
    }
}