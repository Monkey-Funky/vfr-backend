namespace Application.Features.Customer.Auth.Commands.LoginWithGoogle;

/// <summary>
/// Validator for the Google OAuth login command.
/// </summary>
public sealed class LoginWithGoogleCustomerCommandValidator : AbstractValidator<LoginWithGoogleCustomerCommand>
{
    public LoginWithGoogleCustomerCommandValidator()
    {
        RuleFor(x => x.GoogleIdToken)
            .NotEmpty().WithMessage("Google ID token is required.")
            .MinimumLength(50).WithMessage("Google ID token is suspiciously short.");
    }
}
