using Application.Features.Offers.DTOs;

namespace Application.Features.Offers.Queries.GetOffers;

/// <summary>
/// Returns a paginated, retailer-scoped list of offers.
/// Optional filters: Status, OfferType.
/// </summary>
public sealed record GetOffersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null,
    string? OfferType = null
) : IRequest<PagedResult<OfferDto>>;
