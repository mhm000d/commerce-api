using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class RatingValidator : AbstractValidator<Rating>
{
    public RatingValidator()
    {
        RuleFor(r => r.ProductId)
            .NotEmpty()
            .WithMessage("ProductId is required.");

        RuleFor(r => r.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(r => r.Score)
            .InclusiveBetween(1, 5)
            .WithMessage("Score must be between 1 and 5.");

        RuleFor(r => r.Comment)
            .MaximumLength(200)
            .WithMessage("Comment must not exceed 200 characters.")
            .When(r => r.Comment != null);

        RuleFor(r => r.CreatedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddSeconds(1))
            .WithMessage("CreatedAt cannot be in the future.");
    }
}