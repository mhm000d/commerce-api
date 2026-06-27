using Commerce.Contracts.Account;
using FluentValidation;

namespace Commerce.Application.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Phone)
            .MaximumLength(30)
            .When(x => x.Phone is not null);
    }
}