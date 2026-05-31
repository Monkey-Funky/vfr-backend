using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.GetSimilarProducts;

/// <summary>
/// Returns similar products for a given product.
/// Cache-aside (public data, no user scope): TTL 10 minutes.
/// IsFavorite is overlaid per-user post-cache.
/// </summary>
internal sealed class GetSimilarProductsQueryHandler : IRequestHandler<GetSimilarProductsQuery, List<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetSimilarProductsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<List<ProductCardDto>> Handle(GetSimilarProductsQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"similar:{request.ProductId:N}:{request.Limit}";

        var cachedBase = await _cacheService.GetAsync<List<ProductCardDto>>(cacheKey, cancellationToken);
        List<ProductCardDto> result;

        if (cachedBase is not null)
        {
            result = cachedBase;
        }
        else
        {
            var sourceProduct = await _context.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.Status == ProductStatus.Active, cancellationToken)
                ?? throw new NotFoundException("Product", request.ProductId);

            var query = _context.Products.AsNoTracking()
                .Where(p => p.Id != sourceProduct.Id && p.Status == ProductStatus.Active)
                .Where(p =>
                    (sourceProduct.CategoryId.HasValue && p.CategoryId == sourceProduct.CategoryId.Value) ||
                    (!string.IsNullOrWhiteSpace(sourceProduct.Brand) && p.Brand == sourceProduct.Brand))
                .OrderByDescending(p => p.ViewsCount);

            var products = await query
                .Include(p => p.Images)
                .Take(request.Limit)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var productIds = products.Select(p => p.Id).ToList();
            var categoryIds = products.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToList();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var activeOffers = await _context.Offers.AsNoTracking()
                .Where(o => o.Status == "Active" && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today))
                .Where(o => (o.ProductId.HasValue && productIds.Contains(o.ProductId.Value)) ||
                            (o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value)))
                .ToListAsync(cancellationToken);

            result = products.Select(p =>
            {
                var offer = activeOffers.FirstOrDefault(o => o.ProductId == p.Id) ??
                            activeOffers.FirstOrDefault(o => o.CategoryId == p.CategoryId);
                return p.ToProductCardDto(offer, isFavorite: false);
            }).ToList();

            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(10), cancellationToken);
        }

        // Overlay per-user IsFavorite flags
        if (_currentUserService.IsAuthenticated && _currentUserService.CustomerId.HasValue)
        {
            var customerId = _currentUserService.CustomerId.Value;
            var productIds = result.Select(p => p.Id).ToList();
            var favoriteIds = await _context.CustomerFavorites.AsNoTracking()
                .Where(f => f.CustomerId == customerId && productIds.Contains(f.ProductId))
                .Select(f => f.ProductId)
                .ToListAsync(cancellationToken);

            if (favoriteIds.Count > 0)
            {
                var favoriteSet = favoriteIds.ToHashSet();
                result = result.Select(p => favoriteSet.Contains(p.Id) ? p with { IsFavorite = true } : p).ToList();
            }
        }

        return result;
    }
}
