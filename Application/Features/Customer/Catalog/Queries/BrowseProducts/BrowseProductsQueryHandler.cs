using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Shared.Constants;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Features.Customer.Catalog.Queries.BrowseProducts;

/// <summary>
/// Paginates and filters the public product catalog.
/// Cache-aside (shared, no user scope): TTL 3 minutes.
/// Key is a SHA-256 fingerprint of all query parameters — unique per filter combination.
/// Invalidated by product Create/Update/Delete/Toggle in the retailer domain.
/// IsFavorite is a per-user concern: computed post-cache and NOT stored in the cache.
/// </summary>
internal sealed class BrowseProductsQueryHandler : IRequestHandler<BrowseProductsQuery, PagedResult<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public BrowseProductsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<ProductCardDto>> Handle(
        BrowseProductsQuery request,
        CancellationToken cancellationToken)
    {
        string fingerprint = BuildFingerprint(request);
        string cacheKey = CacheKeys.ProductBrowse(fingerprint);

        // Try shared cache (isFavorite = false for all items — user-specific overlay applied below).
        var cachedBase = await _cacheService.GetAsync<BrowseCachePayload>(cacheKey, cancellationToken);

        // FIX: PagedResult<T> is a class, not a record — cannot use "with" expressions.
        // Build a mutable List<ProductCardDto> that we can update in-place.
        List<ProductCardDto> items;
        int totalCount;
        int pageNumber;
        int pageSize;

        if (cachedBase is not null)
        {
            // Clone the list so the cached payload is never mutated.
            items = cachedBase.Items.ToList();
            totalCount = cachedBase.TotalCount;
            pageNumber = cachedBase.PageNumber;
            pageSize = cachedBase.PageSize;
        }
        else
        {
            var fetched = await FetchFromDatabaseAsync(request, cancellationToken);
            items = fetched.Items.ToList();
            totalCount = fetched.TotalCount;
            pageNumber = fetched.PageNumber;
            pageSize = fetched.PageSize;

            // Cache the base result (isFavorite = false for all).
            await _cacheService.SetAsync(
                cacheKey,
                new BrowseCachePayload(items, totalCount, pageNumber, pageSize),
                TimeSpan.FromMinutes(3),
                cancellationToken);
        }

        // Overlay per-user IsFavorite flags — never stored in the shared cache.
        if (_currentUserService.IsAuthenticated
            && _currentUserService.CustomerId.HasValue
            && items.Count > 0)
        {
            var customerId = _currentUserService.CustomerId.Value;
            var productIds = items.Select(p => p.Id).ToList();

            var favoriteIds = await _context.CustomerFavorites.AsNoTracking()
                .Where(f => f.CustomerId == customerId && productIds.Contains(f.ProductId))
                .Select(f => f.ProductId)
                .ToListAsync(cancellationToken);

            if (favoriteIds.Count > 0)
            {
                var favoriteSet = favoriteIds.ToHashSet();
                for (int i = 0; i < items.Count; i++)
                {
                    if (favoriteSet.Contains(items[i].Id))
                        items[i] = items[i] with { IsFavorite = true };
                }
            }
        }

        return new PagedResult<ProductCardDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    // ─── Private DB fetch ────────────────────────────────────────────────────

    private async Task<PagedResult<ProductCardDto>> FetchFromDatabaseAsync(
        BrowseProductsQuery request,
        CancellationToken cancellationToken)
    {
        var query = _context.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active);

        if (request.RetailerId.HasValue)
            query = query.Where(p => p.RetailerId == request.RetailerId.Value);

        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId.Value);

        if (request.SubCategoryId.HasValue)
            query = query.Where(p => p.SubCategoryId == request.SubCategoryId.Value);

        if (request.MinPrice.HasValue)
            query = query.Where(p => p.Price >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= request.MaxPrice.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm) && request.SearchTerm.Length >= 2)
        {
            query = query.Where(p =>
                EF.Property<NpgsqlTsVector>(p, "search_vector")
                  .Matches(EF.Functions.PlainToTsQuery("english", request.SearchTerm)));
        }

        if (request.Categories is { Length: > 0 })
        {
            var categoryIdsToFilter = await _context.Categories.AsNoTracking()
                .Where(c => request.Categories.Contains(c.Name))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);

            query = categoryIdsToFilter.Count > 0
                ? query.Where(p => p.CategoryId.HasValue && categoryIdsToFilter.Contains(p.CategoryId.Value))
                : query.Where(_ => false);
        }

        if (request.Colors is { Length: > 0 })
            query = query.Where(p => p.AvailableColors != null
                                  && p.AvailableColors.Any(c => request.Colors.Contains(c)));

        if (request.Sizes is { Length: > 0 })
            query = query.Where(p => p.AvailableSizes != null
                                  && p.AvailableSizes.Any(s => request.Sizes.Contains(s)));

        if (request.FabricMaterials is { Length: > 0 })
            query = query.Where(p => p.Material != null && request.FabricMaterials.Contains(p.Material));

        if (request.FabricPatterns is { Length: > 0 })
            query = query.Where(p => p.Pattern != null && request.FabricPatterns.Contains(p.Pattern));

        if (request.Brands is { Length: > 0 })
            query = query.Where(p => p.Brand != null && request.Brands.Contains(p.Brand));

        query = request.SortBy?.ToLowerInvariant() switch
        {
            "price" => request.SortOrder?.ToLowerInvariant() == "desc"
                                ? query.OrderByDescending(p => p.Price)
                                : query.OrderBy(p => p.Price),
            "name" => request.SortOrder?.ToLowerInvariant() == "desc"
                                ? query.OrderByDescending(p => p.Name)
                                : query.OrderBy(p => p.Name),
            "newest" => request.SortOrder?.ToLowerInvariant() == "desc"
                                ? query.OrderByDescending(p => p.CreatedAt)
                                : query.OrderBy(p => p.CreatedAt),
            "mostviewed" => request.SortOrder?.ToLowerInvariant() == "desc"
                                ? query.OrderByDescending(p => p.ViewsCount)
                                : query.OrderBy(p => p.ViewsCount),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        int total = await query.CountAsync(cancellationToken);

        // Project directly into an anonymous shape that includes the primary image URL.
        // Using a correlated subquery is more reliable than Include() + navigation collections
        // with AsNoTracking(): avoids all backing-field / ReadOnlyCollection ambiguity and
        // produces a single SQL SELECT instead of a split query.
        var rawProducts = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Brand,
                p.Price,
                p.CategoryId,
                p.AvailableColors,
                // Correlated subquery — translated by EF Core to a SQL LEFT JOIN / subselect.
                // The global query filter (is_deleted = false) on ProductImage is applied automatically.
                PrimaryImageUrl = _context.ProductImages
                    .Where(i => i.ProductId == p.Id)
                    .OrderBy(i => i.DisplayOrder)
                    .Select(i => i.ImageUrl)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var productIds  = rawProducts.Select(p => p.Id).ToList();
        var categoryIds = rawProducts
            .Where(p => p.CategoryId.HasValue)
            .Select(p => p.CategoryId!.Value)
            .Distinct()
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var activeOffers = await _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active"
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today))
            .Where(o => (o.ProductId.HasValue && productIds.Contains(o.ProductId.Value))
                     || (o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value)))
            .ToListAsync(cancellationToken);

        var dtos = rawProducts.Select(p =>
        {
            var offer = activeOffers.FirstOrDefault(o => o.ProductId == p.Id)
                     ?? activeOffers.FirstOrDefault(o => o.CategoryId == p.CategoryId);

            decimal? discountedPrice = null;
            if (offer != null && p.Price.HasValue)
            {
                discountedPrice = Math.Round(offer.DiscountType == "Percentage"
                    ? p.Price.Value * (1 - offer.DiscountValue / 100)
                    : Math.Max(0, p.Price.Value - offer.DiscountValue), 2);
            }

            return new ProductCardDto(
                p.Id,
                p.Name,
                p.Brand,
                p.Price,
                discountedPrice,
                p.PrimaryImageUrl,   // fetched directly from SQL — reliable even with AsNoTracking
                p.AvailableColors,
                IsFavorite: false    // overlaid per-user after cache read
            );
        }).ToList();

        return new PagedResult<ProductCardDto>
        {
            Items = dtos,
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    // ─── Cache fingerprint ───────────────────────────────────────────────────

    private static string BuildFingerprint(BrowseProductsQuery r)
    {
        var key = JsonSerializer.Serialize(new
        {
            r.RetailerId,
            r.CategoryId,
            r.SubCategoryId,
            r.SearchTerm,
            r.MinPrice,
            r.MaxPrice,
            Colors = r.Colors is null ? null : string.Join(',', r.Colors.OrderBy(x => x)),
            Sizes = r.Sizes is null ? null : string.Join(',', r.Sizes.OrderBy(x => x)),
            Cats = r.Categories is null ? null : string.Join(',', r.Categories.OrderBy(x => x)),
            Mats = r.FabricMaterials is null ? null : string.Join(',', r.FabricMaterials.OrderBy(x => x)),
            Pats = r.FabricPatterns is null ? null : string.Join(',', r.FabricPatterns.OrderBy(x => x)),
            Brands = r.Brands is null ? null : string.Join(',', r.Brands.OrderBy(x => x)),
            r.SortBy,
            r.SortOrder,
            r.PageNumber,
            r.PageSize
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash)[..16]; // 16 hex chars = 64-bit fingerprint, collision-free in practice
    }

    // ─── Private cache payload ───────────────────────────────────────────────

    /// <summary>
    /// Serialisation DTO stored in Redis.
    /// Uses <see cref="IReadOnlyList{T}"/> to match <see cref="PagedResult{T}.Items"/>.
    /// isFavorite is always false here; it is overlaid per-user after the cache read.
    /// </summary>
    private sealed record BrowseCachePayload(
        IReadOnlyList<ProductCardDto> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
