using Domain.Enums.Offer;

namespace Application.Features.Offers.Queries.GetOffers;

public sealed class GetOffersQueryValidator : AbstractValidator<GetOffersQuery>
{
    public GetOffersQueryValidator()
    {
        // ── Pagination ────────────────────────────────────────────────────────
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.");

        // ── Optional Status filter ────────────────────────────────────────────
        RuleFor(x => x.Status)
            .Must(s => OfferStatus.IsValid(s!))
            .WithMessage($"Status must be one of: {string.Join(", ", OfferStatus.All)}.")
            .When(x => x.Status is not null);

        // ── Optional OfferType filter ─────────────────────────────────────────
        RuleFor(x => x.OfferType)
            .Must(t => OfferType.IsValid(t!))
            .WithMessage($"OfferType must be one of: {string.Join(", ", OfferType.All)}.")
            .When(x => x.OfferType is not null);
    }
}