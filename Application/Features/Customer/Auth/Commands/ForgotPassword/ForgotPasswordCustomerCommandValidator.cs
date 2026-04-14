namespace Application.Features.Customer.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordCustomerCommandValidator : AbstractValidator<ForgotPasswordCustomerCommand>
{
    public ForgotPasswordCustomerCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(200).WithMessage("Email address must not exceed 200 characters.");
    }
}
