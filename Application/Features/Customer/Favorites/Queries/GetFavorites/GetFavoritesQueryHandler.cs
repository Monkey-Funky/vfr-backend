using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Customer.Favorites.Queries.GetFavorites;

/// <summary>
/// Returns paginated favorites for the authenticated customer.
/// Cache-aside: TTL 5 minutes. Invalidated by AddFavorite and RemoveFavorite.
/// </summary>
internal sealed class GetFavoritesQueryHandler : IRequestHandler<GetFavoritesQuery, PagedResult<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetFavoritesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<ProductCardDto>> Handle(GetFavoritesQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedAccessException("Only authenticated customers can view favorites.");

        string cacheKey = $"{CacheKeys.CustomerFavoritesList(customerId)}:p{request.PageNumber}s{request.PageSize}";

        var cached = await _cacheService.GetAsync<PagedResult<ProductCardDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // 1. Fetch paginated favorite IDs
        var query = _context.CustomerFavorites
            .AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var favoritedProductIds = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(f => f.ProductId)
            .ToListAsync(cancellationToken);

        if (favoritedProductIds.Count == 0)
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

        // 2. Fetch hydrated product data + active offers in parallel
        var productsTask = _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => favoritedProductIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Start offers query after getting product IDs (needed for offer filter)
        var products = await productsTask;
        var activeProductIds = products.Select(p => p.Id).ToList();

        var activeOffers = await _context.Offers
            .AsNoTracking()
            .Where(o => o.Status == Domain.Enums.Offer.OfferStatus.Active
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today)
                     && o.OfferType == Domain.Enums.Offer.OfferType.Product
                     && o.ProductId.HasValue
                     && activeProductIds.Contains(o.ProductId.Value))
            .ToDictionaryAsync(o => o.ProductId!.Value, cancellationToken);

        // 3. Map results, preserving the sorted-by-recency order of favorites
        var resultItems = new List<ProductCardDto>();
        foreach (var favProductId in favoritedProductIds)
        {
            var p = products.FirstOrDefault(x => x.Id == favProductId);
            if (p is not null)
            {
                activeOffers.TryGetValue(p.Id, out var offer);
                resultItems.Add(p.ToProductCardDto(offer, isFavorite: true));
            }
        }

        var result = new PagedResult<ProductCardDto>
        {
            Items = resultItems,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        return result;
    }
}
