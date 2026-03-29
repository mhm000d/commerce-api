using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class CartValidator : AbstractValidator<Cart>
{
    public CartValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleForEach(x => x.Items)
            .SetValidator(new CartItemValidator());

        // prevent duplicate products (extra safety)
        RuleFor(x => x.Items)
            .Must(items =>
                items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Cart cannot contain duplicate products.");
    }
}