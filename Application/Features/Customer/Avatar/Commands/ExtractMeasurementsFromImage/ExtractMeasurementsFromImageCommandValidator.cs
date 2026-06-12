using FluentValidation;

namespace Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;

/// <summary>
/// Stateless, in-memory validation per 05-ValidationConventions §1.
/// No DB access — existence and ownership checks live in the handler.
/// </summary>
public sealed class ExtractMeasurementsFromImageCommandValidator
    : AbstractValidator<ExtractMeasurementsFromImageCommand>
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

    public ExtractMeasurementsFromImageCommandValidator()
    {
        RuleFor(x => x.FrontImage)
            .NotNull().WithMessage("Front image is required.");

        RuleFor(x => x.SideImage)
            .NotNull().WithMessage("Side image is required.");

        RuleFor(x => x.FrontImage.Length)
            .GreaterThan(0).WithMessage("Front image file must not be empty.")
            .LessThanOrEqualTo(MaxImageSizeBytes)
            .WithMessage($"Front image file must not exceed {MaxImageSizeBytes / (1024 * 1024)} MB.")
            .When(x => x.FrontImage is not null);

        RuleFor(x => x.SideImage.Length)
            .GreaterThan(0).WithMessage("Side image file must not be empty.")
            .LessThanOrEqualTo(MaxImageSizeBytes)
            .WithMessage($"Side image file must not exceed {MaxImageSizeBytes / (1024 * 1024)} MB.")
            .When(x => x.SideImage is not null);

        RuleFor(x => x.HeightCm)
            .GreaterThan(0).WithMessage("Height must be greater than zero.")
            .LessThanOrEqualTo(300).WithMessage("Height must not exceed 300 cm.");
    }
}
