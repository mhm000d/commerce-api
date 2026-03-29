using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class CartItemValidator : AbstractValidator<CartItem>
{
    public CartItemValidator()
    {
        RuleFor(x => x.CartId)
            .NotEmpty();

        RuleFor(x => x.ProductId)
            .NotEmpty();

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 999)
            .WithMessage("Quantity must be between 1 and 999.");

        RuleFor(x => x.UnitPriceSnapshot)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price must be >= 0.");
    }
}