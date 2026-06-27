using Commerce.Contracts.Account;
using FluentValidation;

namespace Commerce.Application.Validators;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Za-z]").WithMessage("New password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("New password must contain at least one number.");
    }
}