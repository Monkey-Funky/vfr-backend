namespace Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Validates that both token fields are present.
/// Token validity (signature, expiry, DB match) is verified in the handler.
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Access token is required.");

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
