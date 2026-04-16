namespace Application.Features.Customer.Auth.Commands.CompleteProfile;

public sealed class CompleteCustomerProfileCommandValidator : AbstractValidator<CompleteCustomerProfileCommand>
{
    public CompleteCustomerProfileCommandValidator()
    {
        RuleFor(x => x.TempStepToken)
            .NotEmpty().WithMessage("A valid registration step token is required. " +
                                   "Please restart registration from Step 1.");

        RuleFor(x => x.DateOfBirth)
            .Must(dob => dob <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-13))
            .WithMessage("You must be at least 13 years old to use the platform.");

        RuleFor(x => x.Gender)
            .NotEmpty().WithMessage("Gender selection is required.")
            .Must(g => new[] { "Male", "Female", "Other", "PreferNotToSay" }.Contains(g))
            .WithMessage("Invalid gender selection.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format.")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}
