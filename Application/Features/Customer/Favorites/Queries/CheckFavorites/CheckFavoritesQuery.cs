namespace Application.Features.Customer.Favorites.Queries.CheckFavorites;

public sealed record CheckFavoritesQuery(Guid[] ProductIds) : IRequest<Dictionary<Guid, bool>>;
