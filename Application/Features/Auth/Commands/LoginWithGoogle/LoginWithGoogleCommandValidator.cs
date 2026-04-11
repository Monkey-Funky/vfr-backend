namespace Application.Features.Auth.Commands.LoginWithGoogle;

/// <summary>
/// Stateless field-level validation for LoginWithGoogleCommand.
///
/// Validates that the GoogleIdToken field is present and within a reasonable
/// length limit before the handler calls IGoogleAuthService.ValidateAsync.
///
/// Without this validator, an empty or malformed token string reaches the
/// Google SDK and produces an internal InvalidJwtException — wrapped as
/// ExternalServiceException (HTTP 502) instead of a clean HTTP 422 response.
///
/// Token structure validation (signature, expiry, audience) is performed by
/// IGoogleAuthService.ValidateAsync — that belongs in the handler, not here.
/// </summary>
public sealed class LoginWithGoogleCommandValidator
    : AbstractValidator<LoginWithGoogleCommand>
{
    // Google ID tokens (JWTs) can be long due to claims + signature,
    // but 4096 characters is a generous safe upper bound.
    private const int MaxTokenLength = 4096;

    public LoginWithGoogleCommandValidator()
    {
        RuleFor(x => x.GoogleIdToken)
            .NotEmpty()
            .WithMessage("Google ID token is required.")
            .MaximumLength(MaxTokenLength)
            .WithMessage($"Google ID token must not exceed {MaxTokenLength} characters.");
    }
}