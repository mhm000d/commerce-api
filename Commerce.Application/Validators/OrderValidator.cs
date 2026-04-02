using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.OrderNumber)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must contain at least one item.");

        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemValidator());

        RuleFor(x => x.ShippingAddressSnapshot)
            .NotNull()
            .SetValidator(new AddressSnapshotValidator());
    }
}