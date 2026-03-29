using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class AddressValidator : AbstractValidator<Address>
{
    public AddressValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
        
        RuleFor(x => x.FullName)
            .NotEmpty()
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .MaximumLength(200);

        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .MaximumLength(30)
            .Matches(@"^\+?\d{7,15}$") // simple international format
            .WithMessage("Phone number must be valid.");

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

        // ── Optional Fields ──────────────────────
        RuleFor(x => x.BuildingNumber)
            .MaximumLength(20)
            .When(x => x.BuildingNumber != null);

        RuleFor(x => x.Floor)
            .MaximumLength(20)
            .When(x => x.Floor != null);

        RuleFor(x => x.Apartment)
            .MaximumLength(20)
            .When(x => x.Apartment != null);

        RuleFor(x => x.AddressName)
            .MaximumLength(255)
            .When(x => x.AddressName != null);
    }
}