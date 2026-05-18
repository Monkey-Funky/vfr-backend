using Application.Features.Customer.Outfits.DTOs;

namespace Application.Features.Customer.Outfits.Queries.GetOutfits;

/// <summary>
/// Returns all outfits for the currently authenticated customer, wrapped in a
/// <see cref="PagedResult{T}"/> so that the API surface is consistent with other
/// list endpoints and the integration-test assertions deserialise correctly.
/// </summary>
public sealed record GetOutfitsQuery() : IRequest<PagedResult<OutfitSummaryDto>>;