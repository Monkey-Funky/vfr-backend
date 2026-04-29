using Application.Features.Customer.Outfits.DTOs;

namespace Application.Features.Customer.Outfits.Queries.GetOutfitDetail;

public sealed record GetOutfitDetailQuery(Guid OutfitId) : IRequest<OutfitDetailDto>;
