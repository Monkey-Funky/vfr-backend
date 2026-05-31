using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Favorites.Queries.CheckFavorites;

/// <summary>
/// Returns a dictionary of ProductId → IsFavorite for a batch of product IDs.
/// Cache-aside: TTL 2 minutes (short — favorites change often via ToggleFavorite).
/// </summary>
internal sealed class CheckFavoritesQueryHandler : IRequestHandler<CheckFavoritesQuery, Dictionary<Guid, bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public CheckFavoritesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Dictionary<Guid, bool>> Handle(
        CheckFavoritesQuery request,
        CancellationToken cancellationToken)
    {
        // Ensure we always return a result for every requested ID.
        var result = request.ProductIds.Distinct().ToDictionary(id => id, _ => false);

        var customerId = _currentUserService.CustomerId;
        if (customerId is null || request.ProductIds.Length == 0)
            return result;

        // Build a stable cache key from the sorted product IDs.
        var sortedIds = request.ProductIds.OrderBy(id => id).Select(id => id.ToString("N"));
        string cacheKey = $"check_favorites:{customerId.Value:N}:{string.Join(',', sortedIds)}";

        var cached = await _cacheService.GetAsync<Dictionary<Guid, bool>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var favoritedIds = await _context.CustomerFavorites
            .AsNoTracking()
            .Where(f => f.CustomerId == customerId && request.ProductIds.Contains(f.ProductId))
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var id in favoritedIds)
            result[id] = true;

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);

        return result;
    }
}
