namespace Application.Features.Customer.Auth.Commands.Login;
public sealed class LoginCustomerCommandValidator : AbstractValidator<LoginCustomerCommand>
{
    public LoginCustomerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email address is required.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
    }
}
