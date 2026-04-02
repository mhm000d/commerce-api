using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class AddressSnapshotValidator : AbstractValidator<AddressSnapshot>
{
    public AddressSnapshotValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^(?:\+20|0)?1[0-25]\d{8}$");

        RuleFor(x => x.Country)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Governorate)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Area)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Street)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.BuildingNumber)
            .MaximumLength(20)
            .When(x => x.BuildingNumber != null);

        RuleFor(x => x.Floor)
            .MaximumLength(20)
            .When(x => x.Floor != null);

        RuleFor(x => x.Apartment)
            .MaximumLength(20)
            .When(x => x.Apartment != null);
    }
}