namespace Application.Features.Auth.Commands.ResetPassword;

/// <summary>
/// Stateless field-level validation for ResetPasswordCommand.
/// OTP validity (hash match, expiry) is checked in the handler via Redis.
/// </summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        // Email — standard chain
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(200).WithMessage("Email address must not exceed 200 characters.");

        // OTP — exactly 6 digits
        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("The OTP code is required.")
            .Length(6).WithMessage("OTP code must be exactly 6 digits.")
            .Matches(@"^\d{6}$").WithMessage("OTP code must contain only digits.");

        // New password — full strength chain from 05-ValidationConventions.md §2.2
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Must(p => p.Any(char.IsUpper)
                    && p.Any(char.IsLower)
                    && p.Any(char.IsDigit)
                    && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage(
                "Password must contain at least one uppercase letter, one lowercase letter, " +
                "one digit, and one special character.");
    }
}
