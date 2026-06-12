using Application.Features.Customer.Catalog.DTOs; 

namespace Application.Features.Customer.OutfitSuggestions.Queries.GetComplementaryOutfits;

public sealed record GetComplementaryOutfitsQuery(
    Guid ProductId,
    int TopK = 4
) : IRequest<List<ProductCardDto>>;
