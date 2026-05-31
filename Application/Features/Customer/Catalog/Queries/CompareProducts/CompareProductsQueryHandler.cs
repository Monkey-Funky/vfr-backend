using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.CompareProducts;

/// <summary>
/// Returns side-by-side comparison data for 2–4 products.
/// Cache-aside: TTL 5 minutes. Key is built from the sorted product IDs.
/// The same products always produce the same comparison, regardless of request order.
/// </summary>
internal sealed class CompareProductsQueryHandler
    : IRequestHandler<CompareProductsQuery, List<ProductComparisonDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public CompareProductsQueryHandler(
        IApplicationDbContext context,
        ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<List<ProductComparisonDto>> Handle(
        CompareProductsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.ProductIds is null || request.ProductIds.Length < 2 || request.ProductIds.Length > 4)
            throw new BusinessRuleException(
                "INVALID_COMPARISON_COUNT",
                "You must select between 2 and 4 products to compare.");

        // Build a stable, order-independent cache key from the sorted product IDs.
        var sortedIds = request.ProductIds.OrderBy(id => id).Select(id => id.ToString("N"));
        string cacheKey = $"compare:{string.Join('_', sortedIds)}";

        var cached = await _cacheService.GetAsync<List<ProductComparisonDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var products = await _context.Products.AsNoTracking()
            .Include(p => p.Images)
            .Where(p => request.ProductIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (products.Count != request.ProductIds.Length)
        {
            var foundIds = products.Select(p => p.Id).ToHashSet();
            var missingIds = request.ProductIds.Where(id => !foundIds.Contains(id));
            throw new BusinessRuleException(
                "PRODUCTS_UNAVAILABLE",
                $"Cannot compare. The following products are missing or inactive: {string.Join(", ", missingIds)}");
        }

        var productIds = products.Select(p => p.Id).ToList();
        var categoryIds = products
            .Where(p => p.CategoryId.HasValue)
            .Select(p => p.CategoryId!.Value)
            .Distinct()
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Parallelise offers + inventory fetches.
        var offersTask = _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active"
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today))
            .Where(o => (o.ProductId.HasValue && productIds.Contains(o.ProductId.Value))
                     || (o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value)))
            .ToListAsync(cancellationToken);

        var inventoriesTask = _context.InventoryRecords.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        await Task.WhenAll(offersTask, inventoriesTask);

        var activeOffers = await offersTask;
        var inventories = await inventoriesTask;

        var dtos = products.Select(p =>
        {
            var offer = activeOffers.FirstOrDefault(o => o.ProductId == p.Id)
                     ?? activeOffers.FirstOrDefault(o => o.CategoryId == p.CategoryId);

            string stockStatus = "Out of Stock";
            if (inventories.TryGetValue(p.Id, out var inventory))
            {
                if (inventory.CurrentStock <= 0) stockStatus = "Out of Stock";
                else if (inventory.CurrentStock <= inventory.LowStockThreshold) stockStatus = "Low Stock";
                else stockStatus = "In Stock";
            }

            return p.ToProductComparisonDto(offer, stockStatus);
        }).ToList();

        // Preserve the request order.
        var orderedDtos = request.ProductIds
            .Select(id => dtos.FirstOrDefault(d => d.Id == id))
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();

        await _cacheService.SetAsync(cacheKey, orderedDtos, TimeSpan.FromMinutes(5), cancellationToken);

        return orderedDtos;
    }
}
