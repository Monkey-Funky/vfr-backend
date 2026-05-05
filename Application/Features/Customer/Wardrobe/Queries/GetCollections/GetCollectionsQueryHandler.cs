using Application.Features.Customer.Wardrobe.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Features.Customer.Wardrobe.Mappings;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Queries.GetCollections;

internal sealed class GetCollectionsQueryHandler : IRequestHandler<GetCollectionsQuery, List<WardrobeCollectionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    public GetCollectionsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<WardrobeCollectionDto>> Handle(GetCollectionsQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view collections.");
        
        // 1. Fetch collections 
        var collectionsData = await _context.WardrobeCollections
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                Collection = c,
                // PostgreSQL will execute a COUNT() subquery
                ItemCount = _context.WardrobeCollectionItems.Count(i => i.CollectionId == c.Id),

                // PostgreSQL will execute a subquery to grab JUST the first ProductId
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
            return new List<WardrobeCollectionDto>();
        }

        // 2. Fetch Cover Images ONLY for the needed products 
        var coverProductIds = collectionsData
            .Where(c => c.FirstProductId != Guid.Empty)
            .Select(c => c.FirstProductId)
            .Distinct()
            .ToList();

        var coverImages = new Dictionary<Guid, string>();

        if (coverProductIds.Count > 0)
        {
            coverImages = await _context.ProductImages
                .AsNoTracking()
                .Where(pi => coverProductIds.Contains(pi.ProductId))
                .OrderBy(pi => pi.DisplayOrder)
                .GroupBy(pi => pi.ProductId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.Select(x => x.ImageUrl).FirstOrDefault() ?? string.Empty,
                    cancellationToken
                );
        }

        // 3. Hydrate the final result
        var result = new List<WardrobeCollectionDto>();
        foreach (var data in collectionsData)
        {
            string? coverImageUrl = null;
            if (data.FirstProductId != Guid.Empty)
            {
                coverImages.TryGetValue(data.FirstProductId, out coverImageUrl);
            }

            result.Add(data.Collection.ToDto(data.ItemCount, coverImageUrl));
        }

        return result;
    }
}
