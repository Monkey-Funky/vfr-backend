namespace Application.Features.Customer.Profile.Commands.UpdateProfile;

public sealed class UpdateCustomerProfileCommandValidator : AbstractValidator<UpdateCustomerProfileCommand>
{
    public UpdateCustomerProfileCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required.")
            .MaximumLength(100).WithMessage("Full Name cannot exceed 100 characters.");

        RuleFor(x => x.DateOfBirth)
            .Must(dob => !dob.HasValue || dob.Value >= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-120))
            .WithMessage("Date of birth is unreasonably old.")
            .Must(dob => !dob.HasValue || dob.Value <= DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-13))
            .WithMessage("If provided, you must be at least 13 years old.");

        RuleFor(x => x.Gender)
            .Must(g => string.IsNullOrEmpty(g) || new[] { "Male", "Female", "Other", "PreferNotToSay" }.Contains(g))
            .WithMessage("Invalid gender selection.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
