using Commerce.Application.Models;
using FluentValidation;

namespace Commerce.Application.Validators;

public class UserValidator : AbstractValidator<User>
{
    public UserValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(255)
            .EmailAddress();

        RuleFor(x => x.Phone)
            .MaximumLength(30)
            .When(x => x.Phone is not null);

        RuleFor(x => x.PasswordHash)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(255);

        RuleFor(x => x.Role)
            .IsInEnum();

        RuleFor(x => x.CreatedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow);
    }
}