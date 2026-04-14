namespace Application.Features.Customer.Profile.Commands.ChangePassword;

public sealed class ChangeCustomerPasswordCommandValidator : AbstractValidator<ChangeCustomerPasswordCommand>
{
    public ChangeCustomerPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("Minimum length is 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Must contain at least one number.")
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from the current password.");
    }
}
