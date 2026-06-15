using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Wardrobe.Queries.GetCollectionItems;

/// <summary>
/// Returns paginated products inside a wardrobe collection.
/// Cache-aside: TTL 5 minutes. Invalidated by AddItemToCollection and RemoveItemFromCollection.
/// </summary>
internal sealed class GetCollectionItemsQueryHandler
    : IRequestHandler<GetCollectionItemsQuery, PagedResult<ProductCardDto>>
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

    public async Task<PagedResult<ProductCardDto>> Handle(
        GetCollectionItemsQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view collections.");

        string cacheKey =
            $"wardrobe_items:{customerId:N}:{request.CollectionId:N}:p{request.PageNumber}s{request.PageSize}";

        var cached = await _cacheService.GetAsync<PagedResult<ProductCardDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // IDOR guard: verify collection belongs to this customer.
        var collectionExists = await _context.WardrobeCollections
            .AnyAsync(c => c.Id == request.CollectionId && c.CustomerId == customerId, cancellationToken);

        if (!collectionExists)
            throw new NotFoundException("WardrobeCollection", request.CollectionId);

        // Paginate the product IDs via the join.
        var query = _context.WardrobeCollectionItems
            .AsNoTracking()
            .Where(i => i.CollectionId == request.CollectionId)
            .Join(
                _context.CustomerFavorites.AsNoTracking(),
                i => i.FavoriteId,
                f => f.Id,
                (i, f) => new { i.CreatedAt, f.ProductId })
            .OrderByDescending(x => x.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var productIds = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => x.ProductId)
            .ToListAsync(cancellationToken);

        if (productIds.Count == 0)
        {
            var empty = new PagedResult<ProductCardDto>
            {
                Items = [],
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
            await _cacheService.SetAsync(cacheKey, empty, TimeSpan.FromMinutes(5), cancellationToken);
            return empty;
        }

        // FIX: EF Core DbContext is NOT thread-safe — Task.WhenAll on the same context instance
        // causes "A second operation was started on this context before a previous operation completed"
        // (InvalidOperationException → 500). Queries must be awaited sequentially.
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var activeOffers = await _context.Offers
            .AsNoTracking()
            .Where(o => o.Status == "Active"
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today)
                     && o.ProductId.HasValue
                     && productIds.Contains(o.ProductId.Value))
            .ToListAsync(cancellationToken);

        // Preserve the collection-order (by item CreatedAt) from productIds.
        var productDict = products.ToDictionary(p => p.Id);

        var dtos = productIds
            .Where(id => productDict.ContainsKey(id))
            .Select(id =>
            {
                var p = productDict[id];
                var offer = activeOffers.FirstOrDefault(o => o.ProductId == p.Id);
                return p.ToProductCardDto(offer, isFavorite: true); // always favorited — it's in their collection
            })
            .ToList();

        var result = new PagedResult<ProductCardDto>
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