namespace Application.Features.Customer.Auth.Commands.Register;
public sealed class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(200).WithMessage("Email address must not exceed 200 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Must(p => p.Any(char.IsUpper)
                     && p.Any(char.IsLower)
                     && p.Any(char.IsDigit)
                     && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage("Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character.");
    }
}

