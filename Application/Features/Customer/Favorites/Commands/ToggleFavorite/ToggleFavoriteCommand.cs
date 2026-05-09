namespace Application.Features.Customer.Favorites.Commands.ToggleFavorite;

public sealed record ToggleFavoriteCommand(Guid ProductId) : IRequest<(bool IsSuccess, bool IsFavoriteNow)>;
