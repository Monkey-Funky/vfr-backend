
namespace Application.Features.Settings.Commands.ChangePassword;

/// <summary>
/// Validates the <see cref="ChangePasswordCommand"/>.
/// The current password is only checked structurally here (NotEmpty).
/// BCrypt verification of the actual credential is performed in the handler.
/// </summary>
public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        // ── CurrentPassword ─────────────────────────────────────────────────
        // Only NotEmpty — revealing strength-rule failures for an existing password
        // would help an attacker infer account state. BCrypt verify runs in the handler.
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        // ── NewPassword ──────────────────────────────────────────────────────
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters.")
            .Must(p => p.Any(char.IsUpper)
                     && p.Any(char.IsLower)
                     && p.Any(char.IsDigit)
                     && p.Any(c => !char.IsLetterOrDigit(c)))
            .WithMessage(
                "New password must contain at least one uppercase letter, " +
                "one lowercase letter, one digit, and one special character.");

        // ── ConfirmNewPassword ───────────────────────────────────────────────
        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(x => x.NewPassword)
            .WithMessage("The new password and confirmation password do not match.");

        // ── Prevent reuse of current password as new password ────────────────
        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword)
            .WithMessage("New password must differ from the current password.");
    }
}

