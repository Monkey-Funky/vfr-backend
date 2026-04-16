namespace Application.Features.Customer.Auth.Commands.ResetPassword;

public sealed class ResetPasswordCustomerCommandValidator : AbstractValidator<ResetPasswordCustomerCommand>
{
    public ResetPasswordCustomerCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("Reset code is required.")
            .Length(6).WithMessage("Reset code must be 6 digits.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Must(p => p.Any(char.IsUpper)
                     && p.Any(char.IsLower)
                     && p.Any(char.IsDigit)
                     && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage("Password must contain at least one uppercase letter, one lowercase letter," +
            " one digit, and one special character.");
    }
}
