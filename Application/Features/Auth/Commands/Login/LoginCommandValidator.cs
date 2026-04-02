namespace Application.Features.Auth.Commands.Login;

/// <summary>
/// Stateless field-level validation for LoginCommand.
/// Database-dependent checks (account exists, password matches) are in the handler.
///
/// IMPORTANT: The Password field intentionally uses .NotEmpty() ONLY.
/// Applying strength rules here would leak information about which passwords
/// are structurally invalid before authentication even runs.
/// </summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Email — standard chain from 05-ValidationConventions.md §2.1
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(200).WithMessage("Email address must not exceed 200 characters.");

        // Password — login field: NotEmpty only (intentionally minimal)
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}