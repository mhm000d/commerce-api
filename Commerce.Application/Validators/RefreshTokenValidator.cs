using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class RefreshTokenValidator : AbstractValidator<RefreshToken>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.TokenHash)
            .NotEmpty()
            .MaximumLength(64); // SHA-256 hex output is always 64 chars

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow);

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(x => x.CreatedAt);

        RuleFor(x => x.RevokedAt)
            .GreaterThan(x => x.CreatedAt)
            .When(x => x.RevokedAt.HasValue);
    }
}