using Application.Features.Customer.Wardrobe.DTOs;
using Application.Features.Customer.Wardrobe.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Queries.GetCollections;

/// <summary>
/// Returns all wardrobe collections for the authenticated customer.
/// Cache-aside: TTL 5 minutes. Invalidated by CreateCollection, UpdateCollection, DeleteCollection.
/// </summary>
internal sealed class GetCollectionsQueryHandler : IRequestHandler<GetCollectionsQuery, List<WardrobeCollectionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCollectionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<WardrobeCollectionDto>> Handle(GetCollectionsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view collections.");

        string cacheKey = $"wardrobe:{customerId:N}";

        var cached = await _cacheService.GetAsync<List<WardrobeCollectionDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // 1. Fetch collections with item counts and first product ID in one query
        var collectionsData = await _context.WardrobeCollections
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                Collection = c,
                ItemCount = _context.WardrobeCollectionItems.Count(i => i.CollectionId == c.Id),
                FirstProductId = _context.WardrobeCollectionItems
                    .Where(i => i.CollectionId == c.Id)
                    .OrderBy(i => i.CreatedAt)
                    .Join(_context.CustomerFavorites,
                          i => i.FavoriteId,
                          f => f.Id,
                          (i, f) => f.ProductId)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        if (collectionsData.Count == 0)
        {
            var empty = new List<WardrobeCollectionDto>();
            await _cacheService.SetAsync(cacheKey, empty, TimeSpan.FromMinutes(5), cancellationToken);
            return empty;
        }

        // 2. Fetch cover images only for the needed products
        var coverProductIds = collectionsData
            .Where(c => c.FirstProductId != Guid.Empty)
            .Select(c => c.FirstProductId)
            .Distinct()
            .ToList();

        var coverImages = coverProductIds.Count > 0
            ? await _context.ProductImages.AsNoTracking()
                .Where(pi => coverProductIds.Contains(pi.ProductId))
                .OrderBy(pi => pi.DisplayOrder)
                .GroupBy(pi => pi.ProductId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(x => x.ImageUrl).FirstOrDefault() ?? string.Empty,
                    cancellationToken)
            : new Dictionary<Guid, string>();

        // 3. Map result
        var result = collectionsData.Select(data =>
        {
            string? coverImageUrl = null;
            if (data.FirstProductId != Guid.Empty)
                coverImages.TryGetValue(data.FirstProductId, out coverImageUrl);
            return data.Collection.ToDto(data.ItemCount, coverImageUrl);
        }).ToList();

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

        return result;
    }
}
