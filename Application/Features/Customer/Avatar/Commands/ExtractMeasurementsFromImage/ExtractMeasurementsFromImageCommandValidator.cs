using FluentValidation;

namespace Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;

/// <summary>
/// Stateless, in-memory validation per 05-ValidationConventions
/// No DB access — existence and ownership checks live in the handler.
/// </summary>
public sealed class ExtractMeasurementsFromImageCommandValidator
    : AbstractValidator<ExtractMeasurementsFromImageCommand>
{
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB

    public ExtractMeasurementsFromImageCommandValidator()
    {
        RuleFor(x => x.FrontImageFile)
            .NotNull().WithMessage("Front image file is required.");

        RuleFor(x => x.FrontImageFile.Length)
            .GreaterThan(0).WithMessage("Front image file must not be empty.")
            .LessThanOrEqualTo(MaxImageSizeBytes)
            .WithMessage($"Front image file must not exceed {MaxImageSizeBytes / (1024 * 1024)} MB.")
            .When(x => x.FrontImageFile is not null);

        RuleFor(x => x.SideImageFile!.Length)
            .GreaterThan(0).WithMessage("Side image file must not be empty.")
            .LessThanOrEqualTo(MaxImageSizeBytes)
            .WithMessage($"Side image file must not exceed {MaxImageSizeBytes / (1024 * 1024)} MB.")
            .When(x => x.SideImageFile is not null);

        RuleFor(x => x.HeightCm)
            .GreaterThan(0).WithMessage("Height must be greater than zero.")
            .LessThanOrEqualTo(300).WithMessage("Height must not exceed 300 cm.");
    }
}