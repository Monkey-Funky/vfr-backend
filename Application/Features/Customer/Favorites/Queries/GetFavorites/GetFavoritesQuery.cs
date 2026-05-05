using Application.Features.Customer.Catalog.DTOs;

namespace Application.Features.Customer.Favorites.Queries.GetFavorites;

public sealed record GetFavoritesQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PagedResult<ProductCardDto>>;
