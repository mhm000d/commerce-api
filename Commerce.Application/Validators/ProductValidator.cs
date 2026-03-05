using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class ProductValidator : AbstractValidator<Product>
{
    public ProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Product name is required.")
            .MaximumLength(200)
            .WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Product description is required.")
            .MaximumLength(2000)
            .WithMessage("Product description must not exceed 2000 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0)
            .WithMessage("Price must be greater than zero.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Stock quantity cannot be negative.");

        RuleFor(x => x.Category)
            .IsInEnum()
            .WithMessage("Invalid category.");

        // Rating
        RuleFor(x => x.AverageRating)
            .InclusiveBetween(0, 5)
            .When(x => x.AverageRating.HasValue)
            .WithMessage("Average rating must be between 0 and 5.");

        RuleFor(x => x.RatingCount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Rating count cannot be negative.");

        // Specifications
        RuleForEach(x => x.Specifications)
            .ChildRules(spec =>
            {
                spec.RuleFor(s => s.Key)
                    .NotEmpty()
                    .WithMessage("Specification key is required.")
                    .MaximumLength(50);

                spec.RuleFor(s => s.Value)
                    .NotEmpty()
                    .WithMessage("Specification value is required.")
                    .MaximumLength(50);
            });
    }
}