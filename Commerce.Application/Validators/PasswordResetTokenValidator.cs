using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class PasswordResetTokenValidator : AbstractValidator<PasswordResetToken>
{
    public PasswordResetTokenValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.TokenHash)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow);

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(x => x.CreatedAt);

        RuleFor(x => x.UsedAt)
            .GreaterThan(x => x.CreatedAt)
            .When(x => x.UsedAt.HasValue);
    }
}