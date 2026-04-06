

using Domain.Enums.Offer;

namespace Application.Features.Offers.Commands.UpdateOffer;
public sealed class UpdateOfferCommandValidator
    : AbstractValidator<UpdateOfferCommand>
{
    private const long OfferImageMaxBytes = 1 * 1024 * 1024; // 1 MB

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/jpg", "image/png", "image/webp"];

    public UpdateOfferCommandValidator()
    {
        // ── OfferId ───────────────────────────────────────────────────────────
        RuleFor(x => x.OfferId)
            .NotEmpty().WithMessage("OfferId is required.");

        // ── Title ─────────────────────────────────────────────────────────────
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Offer title is required.")
            .MaximumLength(200).WithMessage("Offer title must not exceed 200 characters.");

        // ── Description (optional) ────────────────────────────────────────────
        RuleFor(x => x.Description)
            .MinimumLength(1).WithMessage("Description must not be blank if provided.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);

        // ── DiscountType ──────────────────────────────────────────────────────
        RuleFor(x => x.DiscountType)
            .NotEmpty().WithMessage("DiscountType is required.")
            .Must(DiscountType.IsValid)
            .WithMessage($"DiscountType must be one of: {string.Join(", ", DiscountType.All)}.");

        // ── DiscountValue (common rules) ──────────────────────────────────────
        RuleFor(x => x.DiscountValue)
            .GreaterThan(0).WithMessage("DiscountValue must be greater than zero.")
            .Must(v => decimal.Round(v, 2) == v)
            .WithMessage("DiscountValue must not have more than 2 decimal places.");

        // Percentage-specific range check
        RuleFor(x => x.DiscountValue)
            .InclusiveBetween(1, 100)
            .WithMessage("Percentage discount must be between 1 and 100.")
            .When(x => x.DiscountType == DiscountType.Percentage);

        // Fixed: upper guard only (ceiling vs product price is a handler concern)
        RuleFor(x => x.DiscountValue)
            .LessThanOrEqualTo(999_999)
            .WithMessage("Fixed discount value must not exceed 999,999.")
            .When(x => x.DiscountType == DiscountType.Fixed);

        // ── Status ────────────────────────────────────────────────────────────
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => s == OfferStatus.Active || s == OfferStatus.Inactive)
            .WithMessage("Status must be 'Active' or 'Inactive'. Expired status is managed automatically.");

        // ── Date range ────────────────────────────────────────────────────────
        RuleFor(x => x)
            .Must(cmd => !cmd.EndDate.HasValue || cmd.EndDate.Value > cmd.StartDate)
            .WithMessage("End date must be after start date.")
            .When(cmd => cmd.StartDate != default);

        // ── CoverImage — optional; validated only when a new file is supplied ─
        // null = keep the existing image; no image rules run when null.
        // ImageFileValidator is IFormFile-typed; use inline rules for FileUploadDto.

        RuleFor(x => x.CoverImage!.Length)
            .LessThanOrEqualTo(OfferImageMaxBytes)
            .WithMessage("Cover image must not exceed 1 MB.")
            .When(x => x.CoverImage is not null);

        RuleFor(x => x.CoverImage!.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct.ToLowerInvariant()))
            .WithMessage("Cover image must be a JPEG, PNG, or WEBP file.")
            .When(x => x.CoverImage is not null);
    }
}