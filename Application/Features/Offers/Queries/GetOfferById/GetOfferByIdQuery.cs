
using Application.Features.Offers.DTOs;

namespace Application.Features.Offers.Queries.GetOfferById;

/// <summary>
/// Returns the full details of a single offer by its ID.
/// Retailer-scoped — the IDOR predicate is applied inside the handler.
/// </summary>
public sealed record GetOfferByIdQuery(
    Guid OfferId
) : IRequest<OfferDto>;
