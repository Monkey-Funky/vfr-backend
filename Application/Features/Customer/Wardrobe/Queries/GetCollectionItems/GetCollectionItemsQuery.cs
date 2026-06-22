using Application.Features.Customer.Wardrobe.DTOs;

namespace Application.Features.Customer.Wardrobe.Queries.GetCollectionItems;

public sealed record GetCollectionItemsQuery(Guid CollectionId, int PageNumber = 1, int PageSize = 20)
    : IRequest<PagedResult<CollectionItemDto>>;
