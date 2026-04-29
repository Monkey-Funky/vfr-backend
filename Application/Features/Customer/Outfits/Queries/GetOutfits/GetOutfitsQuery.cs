using Application.Features.Customer.Outfits.DTOs;

namespace Application.Features.Customer.Outfits.Queries.GetOutfits;

public sealed record GetOutfitsQuery() : IRequest<List<OutfitSummaryDto>>;
