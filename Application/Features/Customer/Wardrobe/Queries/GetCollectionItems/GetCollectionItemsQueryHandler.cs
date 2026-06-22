using Application.Features.Customer.Wardrobe.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Queries.GetCollectionItems;

/// <summary>
/// Returns paginated items inside a wardrobe collection.
/// Each item carries its own row UUID (needed by the client for DELETE /items/{id}),
/// the product UUID, and hydrated product fields.
/// Cache-aside: TTL 5 minutes. Invalidated by AddItemToCollection and RemoveItemFromCollection.
/// </summary>
internal sealed class GetCollectionItemsQueryHandler
    : IRequestHandler<GetCollectionItemsQuery, PagedResult<CollectionItemDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetCollectionItemsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<CollectionItemDto>> Handle(
        GetCollectionItemsQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view collections.");

        string cacheKey =
            $"wardrobe_items:{customerId:N}:{request.CollectionId:N}:p{request.PageNumber}s{request.PageSize}";

        var cached = await _cacheService.GetAsync<PagedResult<CollectionItemDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // IDOR guard: verify collection belongs to this customer.
        var collectionExists = await _context.WardrobeCollections
            .AnyAsync(c => c.Id == request.CollectionId && c.CustomerId == customerId, cancellationToken);

        if (!collectionExists)
            throw new NotFoundException("WardrobeCollection", request.CollectionId);

        // Join items → favorites to get the item row UUID, addedAt timestamp, and product ID.
        // i.Id is the WardrobeCollectionItem row UUID the client needs for DELETE /items/{id}.
        var query = _context.WardrobeCollectionItems
            .AsNoTracking()
            .Where(i => i.CollectionId == request.CollectionId)
            .Join(
                _context.CustomerFavorites.AsNoTracking(),
                i => i.FavoriteId,
                f => f.Id,
                (i, f) => new { ItemId = i.Id, AddedAt = i.CreatedAt, f.ProductId })
            .OrderByDescending(x => x.AddedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (page.Count == 0)
        {
            var empty = new PagedResult<CollectionItemDto>
            {
                Items = [],
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
            await _cacheService.SetAsync(cacheKey, empty, TimeSpan.FromMinutes(5), cancellationToken);
            return empty;
        }

        var productIds = page.Select(x => x.ProductId).Distinct().ToList();

        // Correlated subquery for the primary image URL — same pattern as BrowseProductsQueryHandler.
        // Avoids Include + AsSplitQuery and produces a single SQL SELECT per product batch.
        // The global query filter on ProductImages (is_deleted = false) is applied automatically.
        var products = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                PrimaryImageUrl = _context.ProductImages
                    .Where(img => img.ProductId == p.Id)
                    .OrderBy(img => img.DisplayOrder)
                    .Select(img => img.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var productDict = products.ToDictionary(p => p.Id);

        // Preserve the recency order from the paginated page.
        var dtos = page
            .Where(x => productDict.ContainsKey(x.ProductId))
            .Select(x =>
            {
                var p = productDict[x.ProductId];
                return new CollectionItemDto(
                    x.ItemId,
                    x.ProductId,
                    p.Name,
                    p.PrimaryImageUrl,
                    p.Price,
                    x.AddedAt,
                    request.CollectionId
                );
            })
            .ToList();

        var result = new PagedResult<CollectionItemDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return result;
    }
}
