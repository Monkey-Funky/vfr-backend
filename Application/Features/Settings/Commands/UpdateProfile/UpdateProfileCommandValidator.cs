namespace Application.Features.Settings.Commands.UpdateProfile;

/// <summary>
/// Validates the <see cref="UpdateProfileCommand"/> fields.
/// Only validates fields that are non-null (because null means "leave unchanged").
/// BrandName uniqueness is checked in the handler — validators must not touch the database.
/// </summary>
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        // ── FullName ─────────────────────────────────────────────────────────
        When(x => x.FullName is not null, () =>
        {
            RuleFor(x => x.FullName)
                .NotEmpty().WithMessage("Full name must not be empty when provided.")
                .MaximumLength(100).WithMessage("Full name must not exceed 100 characters.");
        });

        // ── PhoneNumber ──────────────────────────────────────────────────────
        When(x => x.PhoneNumber is not null, () =>
        {
            RuleFor(x => x.PhoneNumber)
                .MaximumLength(20).WithMessage("Phone number must not exceed 20 characters.")
                .Matches(@"^[+\d\s\-().]*$")
                .WithMessage("Phone number contains invalid characters.");
        });

        // ── BrandName ────────────────────────────────────────────────────────
        When(x => x.BrandName is not null, () =>
        {
            RuleFor(x => x.BrandName)
                .NotEmpty().WithMessage("Brand name must not be empty when provided.")
                .MaximumLength(150).WithMessage("Brand name must not exceed 150 characters.");
        });

        // ── BusinessType ─────────────────────────────────────────────────────
        When(x => x.BusinessType is not null, () =>
        {
            RuleFor(x => x.BusinessType)
                .NotEmpty().WithMessage("Business type must not be empty when provided.")
                .MaximumLength(50).WithMessage("Business type must not exceed 50 characters.")
                .Must(bt => AllowedBusinessTypes.Contains(bt!))
                .WithMessage(
                    $"Business type must be one of: {string.Join(", ", AllowedBusinessTypes)}.");
        });

        // ── At least one field must be provided ──────────────────────────────
        RuleFor(x => x)
            .Must(x => x.FullName is not null
                    || x.PhoneNumber is not null
                    || x.BrandName is not null
                    || x.BusinessType is not null)
            .WithMessage("At least one field must be provided for a profile update.")
            .OverridePropertyName("Request");
    }

    private static readonly HashSet<string> AllowedBusinessTypes =
    [
        "Fashion", "Electronics", "Furniture", "Beauty", "Sports",
        "Home", "Food", "Books", "Toys", "Jewelry", "Unknown"
    ];
}