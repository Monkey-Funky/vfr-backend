namespace Application.Features.Customer.Auth.Commands.RefreshToken;

public sealed class RefreshCustomerTokenCommandValidator : AbstractValidator<RefreshCustomerTokenCommand>
{
    public RefreshCustomerTokenCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Access token is required.");

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
