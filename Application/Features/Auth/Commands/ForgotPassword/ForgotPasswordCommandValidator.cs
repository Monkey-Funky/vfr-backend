namespace Application.Features.Auth.Commands.ForgotPassword;

/// <summary>
/// Validates the email field format before the handler runs.
/// </summary>
public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(200).WithMessage("Email address must not exceed 200 characters.");
    }
}
