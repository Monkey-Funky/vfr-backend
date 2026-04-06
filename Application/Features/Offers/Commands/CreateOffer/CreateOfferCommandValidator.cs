
using Domain.Enums.Offer;

namespace Application.Features.Offers.Commands.CreateOffer;

/// <summary>
/// Stateless, in-memory validation only (see 05-ValidationConventions §1).
/// DB-dependent checks (entity existence, ownership, price comparisons) are
/// enforced inside CreateOfferCommandHandler.
/// </summary>
public sealed class CreateOfferCommandValidator
    : AbstractValidator<CreateOfferCommand>
{
    private const long OfferImageMaxBytes = 1 * 1024 * 1024; // 1 MB

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/jpg", "image/png", "image/webp"];

    public CreateOfferCommandValidator()
    {
        // ── Title ─────────────────────────────────────────────────────────────
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Offer title is required.")
            .MaximumLength(200).WithMessage("Offer title must not exceed 200 characters.");

        // ── Description (optional) ────────────────────────────────────────────
        RuleFor(x => x.Description)
            .MinimumLength(1).WithMessage("Description must not be blank if provided.")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);

        // ── OfferType ─────────────────────────────────────────────────────────
        RuleFor(x => x.OfferType)
            .NotEmpty().WithMessage("OfferType is required.")
            .Must(OfferType.IsValid)
            .WithMessage($"OfferType must be one of: {string.Join(", ", OfferType.All)}.");

        // ── ProductId / CategoryId (structural non-empty checks only) ─────────
        // Whether they belong to this retailer is validated in the handler.
        RuleFor(x => x.ProductId)
            .NotNull().WithMessage("ProductId is required for a Product-type offer.")
            .NotEqual(Guid.Empty).WithMessage("ProductId must be a valid identifier.")
            .When(x => x.OfferType == OfferType.Product);

        RuleFor(x => x.CategoryId)
            .NotNull().WithMessage("CategoryId is required for a Category-type offer.")
            .NotEqual(Guid.Empty).WithMessage("CategoryId must be a valid identifier.")
            .When(x => x.OfferType == OfferType.Category);
            
        // Mutual exclusivity: a Product-type offer must NOT carry a CategoryId
        RuleFor(x => x.CategoryId)
            .Null().WithMessage("CategoryId must be null for a Product-type offer.")
            .When(x => x.OfferType == OfferType.Product);

        // Mutual exclusivity: a Category-type offer must NOT carry a ProductId
        RuleFor(x => x.ProductId)
            .Null().WithMessage("ProductId must be null for a Category-type offer.")
            .When(x => x.OfferType == OfferType.Category);

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

        // ── StartDate — must not be in the past ───────────────────────────────
        RuleFor(x => x.StartDate)
            .GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.Date))
            .WithMessage("Start date must not be in the past.");

        // ── EndDate (optional) ────────────────────────────────────────────────
        RuleFor(x => x)
            .Must(cmd => !cmd.EndDate.HasValue || cmd.EndDate.Value > cmd.StartDate)
            .WithMessage("End date must be after start date.")
            .When(cmd => cmd.StartDate != default);

        // ── CoverImage — required, validated via FileUploadDto inline rules ───
        // ImageFileValidator is IFormFile-typed; use inline rules for FileUploadDto.
        // Magic byte validation is delegated to IFileStorageService.UploadAsync.

        RuleFor(x => x.CoverImage)
            .NotNull().WithMessage("Cover image is required.");

        RuleFor(x => x.CoverImage.Length)
            .LessThanOrEqualTo(OfferImageMaxBytes)
            .WithMessage("Cover image must not exceed 1 MB.")
            .When(x => x.CoverImage is not null);

        RuleFor(x => x.CoverImage.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct.ToLowerInvariant()))
            .WithMessage("Cover image must be a JPEG, PNG, or WEBP file.")
            .When(x => x.CoverImage is not null);
    }
}
