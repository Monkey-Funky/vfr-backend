namespace Application.Features.Auth.Commands.RegisterStep1;

/// <summary>
/// Stateless field-level validation for RegisterStep1Command.
/// Uniqueness checks (email already exists, brand name taken) are in the handler.
/// </summary>
public sealed class RegisterStep1CommandValidator : AbstractValidator<RegisterStep1Command>
{
    public RegisterStep1CommandValidator()
    {
        // FullName — varchar(100) in DB
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");

        // Email — standard chain from 05-ValidationConventions.md §2.1
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(200).WithMessage("Email address must not exceed 200 characters.");

        // Password — full strength chain from 05-ValidationConventions.md §2.2
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Must(p => p.Any(char.IsUpper)
                    && p.Any(char.IsLower)
                    && p.Any(char.IsDigit)
                    && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage(
                "Password must contain at least one uppercase letter, one lowercase letter, " +
                "one digit, and one special character.");

        // BrandName — varchar(150) in DB; minimum 2 chars per spec
        RuleFor(x => x.BrandName)
            .NotEmpty().WithMessage("Brand name is required.")
            .MinimumLength(2).WithMessage("Brand name must be at least 2 characters.")
            .MaximumLength(150).WithMessage("Brand name must not exceed 150 characters.");
    }
}