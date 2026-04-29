using Application.Features.Customer.Catalog.DTOs;
using Application.Interfaces.Persistence;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Application.Features.Customer.Catalog.Mappings;

namespace Application.Features.Customer.Catalog.Queries.CompareProducts;

internal sealed class CompareProductsQueryHandler : IRequestHandler<CompareProductsQuery, List<ProductComparisonDto>>
{
    private readonly IApplicationDbContext _context;

    public CompareProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductComparisonDto>> Handle(CompareProductsQuery request, CancellationToken cancellationToken)
    {
        if (request.ProductIds == null || request.ProductIds.Length < 2 || request.ProductIds.Length > 4)
            throw new BusinessRuleException("INVALID_COMPARISON_COUNT", "You must select between 2 and 4 products to compare.");

        var products = await _context.Products.AsNoTracking()
            .Include(p => p.Images)
            .Where(p => request.ProductIds.Contains(p.Id) && p.Status == ProductStatus.Active)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        if (products.Count != request.ProductIds.Length)
        {
            // Find exactly which IDs were missing/inactive to give a helpful error
            var foundIds = products.Select(p => p.Id).ToHashSet();
            var missingIds = request.ProductIds.Where(id => !foundIds.Contains(id));

            throw new BusinessRuleException(
                "PRODUCTS_UNAVAILABLE",
                $"Cannot compare. The following products are missing or inactive: {string.Join(", ", missingIds)}");
        }

        var productIds = products.Select(p => p.Id).ToList();
        var categoryIds = products.Where(p => p.CategoryId.HasValue).Select(p => p.CategoryId!.Value).Distinct().ToList();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var activeOffers = await _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active" && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today))
            .Where(o => (o.ProductId.HasValue && productIds.Contains(o.ProductId.Value)) || 
                        (o.CategoryId.HasValue && categoryIds.Contains(o.CategoryId.Value)))
            .ToListAsync(cancellationToken);

        var inventories = await _context.InventoryRecords.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId))
            .ToDictionaryAsync(i => i.ProductId, cancellationToken);

        var dtos = products.Select(p => 
        {
            decimal? discountedPrice = null;
            var offer = activeOffers.FirstOrDefault(o => o.ProductId == p.Id) ?? 
                        activeOffers.FirstOrDefault(o => o.CategoryId == p.CategoryId);

            string stockStatus = "Out of Stock";
            if (inventories.TryGetValue(p.Id, out var inventory))
            {
                if (inventory.CurrentStock <= 0) stockStatus = "Out of Stock";
                else if (inventory.CurrentStock <= inventory.LowStockThreshold) stockStatus = "Low Stock";
                else stockStatus = "In Stock";
            }

            return p.ToProductComparisonDto(offer, stockStatus);
        }).ToList();

        // Order results to match the order of requested IDs if possible
        var orderedDtos = new List<ProductComparisonDto>();
        foreach (var id in request.ProductIds)
        {
            var dto = dtos.FirstOrDefault(d => d.Id == id);
            if (dto != null) orderedDtos.Add(dto);
        }

        return orderedDtos;
    }
}
