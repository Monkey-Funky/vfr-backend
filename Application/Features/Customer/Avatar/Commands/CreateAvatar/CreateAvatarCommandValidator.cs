namespace Application.Features.Customer.Avatar.Commands.CreateAvatar;

public sealed class CreateAvatarCommandValidator : AbstractValidator<CreateAvatarCommand>
{
    public CreateAvatarCommandValidator()
    {
        RuleFor(v => v.HeightCm)
            .GreaterThan(0).WithMessage("Height must be greater than zero.")
            .LessThan(300).WithMessage("Height must be a valid human height.");

        RuleFor(v => v.WeightKg)
            .GreaterThan(0).WithMessage("Weight must be greater than zero.")
            .LessThan(500).WithMessage("Weight must be a valid human weight.");

        RuleFor(v => v.Source)
            .NotEmpty().WithMessage("Source is required.")
            .Must(s => s == "Manual" || s == "BodyScan" || s == "AIEstimate")
            .WithMessage("Source must be Manual, BodyScan, or AIEstimate.");

        RuleFor(v => v.BodyShape)
            .Must(s => string.IsNullOrEmpty(s) || 
                       s == "Rectangle" || s == "Triangle" || 
                       s == "InvertedTriangle" || s == "Hourglass" || 
                       s == "Apple" || s == "Pear")
            .WithMessage("Invalid BodyShape.");
    }
}
