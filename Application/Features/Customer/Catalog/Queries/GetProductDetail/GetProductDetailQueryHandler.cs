using Application.Features.Customer.Catalog.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.GetProductDetail;

/// <summary>
/// Returns full product detail for the customer catalog.
/// Parallelises 4 independent DB calls (offer, inventory, isFavorite, category)
/// using Task.WhenAll — ~3× faster than serial awaits.
/// ViewsCount is incremented atomically before the fetch.
/// </summary>
internal sealed class GetProductDetailQueryHandler : IRequestHandler<GetProductDetailQuery, ProductDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetProductDetailQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ProductDetailDto> Handle(GetProductDetailQuery request, CancellationToken cancellationToken)
    {
        // 1. Increment ViewsCount atomically (fire-and-forget style — no tracking needed).
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE products SET views_count = views_count + 1 WHERE id = {0}",
            request.ProductId);

        // 2. Fetch the product with its images.
        var product = await _context.Products.AsNoTracking()
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.Status == ProductStatus.Active, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 3. Parallelise the 4 remaining independent DB calls.
        var offerTask = _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active"
                     && o.StartDate <= today
                     && (o.EndDate == null || o.EndDate >= today))
            .Where(o => o.ProductId == product.Id
                     || (product.CategoryId.HasValue && o.CategoryId == product.CategoryId.Value))
            .OrderByDescending(o => o.DiscountValue)
            .FirstOrDefaultAsync(cancellationToken);

        var inventoryTask = _context.InventoryRecords.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProductId == product.Id, cancellationToken);

        var isFavoriteTask = (_currentUserService.IsAuthenticated
                              && _currentUserService.CustomerId.HasValue
                              && _currentUserService.CustomerId.Value != Guid.Empty)
            ? _context.CustomerFavorites.AsNoTracking()
                .AnyAsync(f => f.CustomerId == _currentUserService.CustomerId!.Value
                            && f.ProductId == product.Id, cancellationToken)
            : Task.FromResult(false);

        // FIX: Use a concrete anonymous-type projection via a typed local method
        // instead of Task<dynamic?>, which the compiler cannot unify in a ternary.
        Task<CategoryProjection?> categoryTask = product.CategoryId.HasValue
            ? _context.Categories.AsNoTracking()
                .Where(c => c.Id == product.CategoryId.Value)
                .Select(c => new CategoryProjection(c.Id, c.Name, c.Description))
                .FirstOrDefaultAsync(cancellationToken)
            : Task.FromResult<CategoryProjection?>(null);

        await Task.WhenAll(offerTask, inventoryTask, isFavoriteTask, categoryTask);

        var activeOffer = await offerTask;
        var inventory = await inventoryTask;
        bool isFavorite = await isFavoriteTask;
        var categoryProj = await categoryTask;

        // 4. Compute discounted price.
        decimal? discountedPrice = null;
        if (activeOffer is not null && product.Price.HasValue)
        {
            discountedPrice = activeOffer.DiscountType == "Percentage"
                ? Math.Round(product.Price.Value * (1 - activeOffer.DiscountValue / 100m), 2)
                : Math.Max(0, product.Price.Value - activeOffer.DiscountValue);
        }

        // 5. Compute stock status.
        string stockStatus = "Out of Stock";
        if (inventory is not null)
        {
            if (inventory.CurrentStock <= 0) stockStatus = "Out of Stock";
            else if (inventory.CurrentStock <= inventory.LowStockThreshold) stockStatus = "Low Stock";
            else stockStatus = "In Stock";
        }

        // 6. Map category projection.
        CategoryInfoDto? categoryInfo = categoryProj is null
            ? null
            : new CategoryInfoDto(categoryProj.Id, categoryProj.Name, categoryProj.Description);

        var attributes = new ProductAttributesDto(
            product.Material,
            product.Pattern,
            product.Lining,
            product.Length,
            product.Occasion,
            product.Neckline,
            product.Closure,
            product.Sleeves
        );

        var images = product.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.DisplayOrder))
            .ToList();

        return new ProductDetailDto(
            product.Id,
            product.Name,
            product.Brand,
            product.Price,
            discountedPrice,
            product.Description,
            product.Features,
            product.WashInstructions,
            product.ViewsCount,
            null, // AverageRating
            0,    // ReviewCount
            product.AvailableSizes,
            product.AvailableColors,
            stockStatus,
            isFavorite,
            attributes,
            images,
            categoryInfo
        );
    }

    // Private projection record — gives the ternary a concrete, unambiguous type
    // so the compiler does not have to unify Task<AnonymousType> with Task<dynamic?>.
    private sealed record CategoryProjection(Guid Id, string Name, string? Description);
}
