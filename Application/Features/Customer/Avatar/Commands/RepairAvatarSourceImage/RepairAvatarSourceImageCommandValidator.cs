using FluentValidation;

namespace Application.Features.Customer.Avatar.Commands.RepairAvatarSourceImage;

/// <summary>
/// Stateless, in-memory validation for <see cref="RepairAvatarSourceImageCommand"/>.
/// No DB access — existence and ownership checks live in the handler.
/// </summary>
public sealed class RepairAvatarSourceImageCommandValidator
    : AbstractValidator<RepairAvatarSourceImageCommand>
{
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB

    public RepairAvatarSourceImageCommandValidator()
    {
        RuleFor(x => x.FrontImageFile)
            .NotNull().WithMessage("Front image file is required.");

        RuleFor(x => x.FrontImageFile.Length)
            .GreaterThan(0).WithMessage("Front image file must not be empty.")
            .LessThanOrEqualTo(MaxImageSizeBytes)
            .WithMessage($"Front image file must not exceed {MaxImageSizeBytes / (1024 * 1024)} MB.")
            .When(x => x.FrontImageFile is not null);
    }
}
