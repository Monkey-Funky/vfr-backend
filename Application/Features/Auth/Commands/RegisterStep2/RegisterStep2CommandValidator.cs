namespace Application.Features.Auth.Commands.RegisterStep2;

/// <summary>
/// Stateless field-level validation for RegisterStep2Command.
/// Step token validity (signature, expiry, claims) is verified in the handler.
/// </summary>
public sealed class RegisterStep2CommandValidator : AbstractValidator<RegisterStep2Command>
{
    // 2 MB file size limit for brand/profile photos (05-ValidationConventions.md §3.1)
    private const long MaxLogoSizeBytes = 2L * 1024 * 1024;

    public RegisterStep2CommandValidator()
    {
        // TempStepToken — must not be empty (structural check only; validity checked in handler)
        RuleFor(x => x.TempStepToken)
            .NotEmpty().WithMessage("A valid registration step token is required. " +
                                   "Please restart registration from Step 1.");

        // BusinessType — varchar(50) in DB
        RuleFor(x => x.BusinessType)
            .NotEmpty().WithMessage("Business type is required.")
            .MaximumLength(50).WithMessage("Business type must not exceed 50 characters.");

        // Brand logo — optional but if provided, must be a valid image under 2 MB
        When(x => x.BrandLogoStream is not null, () =>
        {
            RuleFor(x => x.BrandLogoFileName)
                .NotEmpty().WithMessage("A file name is required when uploading a brand logo.");

            RuleFor(x => x.BrandLogoContentType)
                .Must(ct => ct is not null && ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Brand logo must be an image file (JPEG, PNG, WebP, GIF).");

            RuleFor(x => x.BrandLogoSizeBytes)
                .GreaterThan(0).WithMessage("Brand logo file must not be empty.")
                .LessThanOrEqualTo(MaxLogoSizeBytes)
                .WithMessage("Brand logo must not exceed 2 MB.");
        });
    }
}