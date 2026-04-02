using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class PaymentValidator : AbstractValidator<Payment>
{
    public PaymentValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEmpty();

        RuleFor(x => x.PaymentProviderId)
            .NotEmpty()
            .MaximumLength(255)
            .Matches(@"^(pi_|ch_)[a-zA-Z0-9]+$")
            .WithMessage("Invalid Stripe payment provider ID.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0.");

        RuleFor(x => x.Status)
            .IsInEnum();

        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow);
    }
}