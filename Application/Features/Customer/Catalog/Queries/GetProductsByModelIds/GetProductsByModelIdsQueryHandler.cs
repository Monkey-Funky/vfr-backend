using Application.Features.Customer.Catalog.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.GetProductsByModelIds;

/// <summary>
/// Resolves a list of AI model IDs (e.g. "78_y3ppkj") to full <see cref="ProductCardDto"/>
/// records for consumption by the front-end after receiving a style-recommendation
/// model response.
///
/// DESIGN NOTES:
///   • Single DB round-trip: fetches all matching products in one query, then
///     resolves favorite status and active offers in two parallel queries.
///   • Preserves input order — the AI model ranks by relevance; we must not
///     re-sort arbitrarily.
///   • Unknown model IDs are silently skipped (graceful degradation).
///   • Only Active products are returned — Inactive / Draft products are excluded
///     even if their model_id matches (consistent with BrowseProducts behaviour).
///   • Favorite status requires an authenticated customer; returns false otherwise.
/// </summary>
internal sealed class GetProductsByModelIdsQueryHandler
    : IRequestHandler<GetProductsByModelIdsQuery, List<ProductCardDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetProductsByModelIdsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ProductCardDto>> Handle(
        GetProductsByModelIdsQuery request,
        CancellationToken cancellationToken)
    {
        // Normalise: trim whitespace and deduplicate while preserving first-seen order.
        var modelIds = request.ModelIds
            .Select(id => id.Trim())
            .Where(id => id.Length > 0)
            .Distinct()
            .ToList();

        if (modelIds.Count == 0)
            return [];

        // ── 1. Fetch all matching Active products in a single round-trip ──────
        //
        // AsNoTracking: read-only projection, no EF change-tracking overhead.
        // AsSplitQuery: avoids cartesian explosion from the Images Include.
        // GlobalQueryFilter (is_deleted = false) is applied automatically.
        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .AsSplitQuery()
            .Where(p => p.ModelId != null
                     && modelIds.Contains(p.ModelId)
                     && p.Status == ProductStatus.Active)
            .ToListAsync(cancellationToken);

        if (products.Count == 0)
            return [];

        var productIds = products.Select(p => p.Id).ToList();

        // ── 2. Parallel: active offers + favorite flags ───────────────────────
        //
        // Both are fire-and-forget until Task.WhenAll; neither depends on the other.

        //var today = DateOnly.FromDateTime(DateTime.UtcNow);

        //// Active offer lookup: keyed by product ID for O(1) lookup in step 4.
        //var offersTask = _context.Offers
        //    .AsNoTracking()
        //    .Where(o => o.Status == "Active"
        //             && o.StartDate <= today
        //             && (o.EndDate == null || o.EndDate >= today)
        //             && (productIds.Contains(o.ProductId!.Value) || o.ProductId == null))
        //    .GroupBy(o => o.ProductId)
        //    .Select(g => g.OrderByDescending(o => o.DiscountValue).First())
        //    .ToListAsync(cancellationToken);

        //// Favorite status: only relevant for authenticated customers.
        //var isFavoriteTask = (_currentUserService.IsAuthenticated
        //                      && _currentUserService.CustomerId.HasValue
        //                      && _currentUserService.CustomerId.Value != Guid.Empty)
        //    ? _context.CustomerFavorites
        //        .AsNoTracking()
        //        .Where(f => f.CustomerId == _currentUserService.CustomerId!.Value
        //                 && productIds.Contains(f.ProductId))
        //        .Select(f => f.ProductId)
        //        .ToListAsync(cancellationToken)
        //    : Task.FromResult(new List<Guid>());

        //await Task.WhenAll(offersTask, isFavoriteTask);


        //var offersByProductId = (await offersTask)
        //    .Where(o => o.ProductId.HasValue)
        //    .ToDictionary(o => o.ProductId!.Value);

        //var favoriteSet = new HashSet<Guid>(await isFavoriteTask);
        // ── 2. Sequential execution (EF Core DbContext is NOT thread-safe) ───────────────

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Fetch the raw, flat offers from the database FIRST
        var rawOffers = await _context.Offers
            .AsNoTracking()
            .Where(o => o.Status == "Active"
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today)
                     && (productIds.Contains(o.ProductId!.Value) || o.ProductId == null))
            .ToListAsync(cancellationToken);

        // 2. Safely group and pick the highest discount IN MEMORY
        var offersByProductId = rawOffers
            .Where(o => o != null && o.ProductId.HasValue) // Bulletproof null check
            .GroupBy(o => o.ProductId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(o => o.DiscountValue).First()
            );

        // 3. THEN fetch favorites...
        var favoriteList = (_currentUserService.IsAuthenticated
                              && _currentUserService.CustomerId.HasValue
                              && _currentUserService.CustomerId.Value != Guid.Empty)
            ? await _context.CustomerFavorites
                .AsNoTracking()
                .Where(f => f.CustomerId == _currentUserService.CustomerId!.Value
                         && productIds.Contains(f.ProductId))
                .Select(f => f.ProductId)
                .ToListAsync(cancellationToken)
            : new List<Guid>();

        var favoriteSet = new HashSet<Guid>(favoriteList);

        // ── 3. Build a lookup keyed by model_id for rank-preserving projection ─
        var productByModelId = products.ToDictionary(p => p.ModelId!);

        // ── 4. Project in the same order as the input model IDs ───────────────
        //
        // We iterate the original (deduplicated) model IDs rather than the DB
        // result list, so the caller's rank ordering is preserved exactly.
        var results = new List<ProductCardDto>(modelIds.Count);

        foreach (var modelId in modelIds)
        {
            if (!productByModelId.TryGetValue(modelId, out var product))
                continue; // model ID not found or product is inactive/deleted — skip

            // Compute discounted price if an active offer exists for this product.
            decimal? discountedPrice = null;
            if (product.Price.HasValue
                && offersByProductId.TryGetValue(product.Id, out var offer))
            {
                discountedPrice = offer.DiscountType == "Percentage"
                    ? Math.Round(product.Price.Value * (1 - offer.DiscountValue / 100m), 2)
                    : Math.Max(0, product.Price.Value - offer.DiscountValue);
            }

            var primaryImageUrl = product.Images
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.DisplayOrder)
                .Select(i => i.ImageUrl)
                .FirstOrDefault();

            results.Add(new ProductCardDto(
                Id: product.Id,
                Name: product.Name,
                BrandName: product.Brand,
                Price: product.Price,
                DiscountedPrice: discountedPrice,
                PrimaryImageUrl: primaryImageUrl,
                AvailableColors: product.AvailableColors,
                IsFavorite: favoriteSet.Contains(product.Id)
            ));
        }

        return results;
    }
}