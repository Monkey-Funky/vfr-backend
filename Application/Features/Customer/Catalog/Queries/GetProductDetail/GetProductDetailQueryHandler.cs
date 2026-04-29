using Application.Features.Customer.Catalog.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.Catalog.Queries.GetProductDetail;

internal sealed class GetProductDetailQueryHandler : IRequestHandler<GetProductDetailQuery, ProductDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetProductDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ProductDetailDto> Handle(GetProductDetailQuery request, CancellationToken cancellationToken)
    {
        // 1. Increment ViewsCount atomically
        // Using raw SQL to avoid EF Core tracking overhead and race conditions
        await _context.Database.ExecuteSqlRawAsync(
            "UPDATE products SET views_count = views_count + 1 WHERE id = {0}", 
            request.ProductId);

        // 2. Fetch Product with NoTracking
        var product = await _context.Products.AsNoTracking()
            .Include(p => p.Images)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.Status == ProductStatus.Active, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductId);

        // 3. Fetch Active Offers
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activeOffer = await _context.Offers.AsNoTracking()
            .Where(o => o.Status == "Active" && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today))
            .Where(o => o.ProductId == product.Id || (product.CategoryId.HasValue && o.CategoryId == product.CategoryId.Value))
            .OrderByDescending(o => o.DiscountValue) // Pick best offer if multiple apply
            .FirstOrDefaultAsync(cancellationToken);

        decimal? discountedPrice = null;
        if (activeOffer != null && product.Price.HasValue)
        {
            discountedPrice = activeOffer.DiscountType == "Percentage"
            ? Math.Round(product.Price.Value * (1 - activeOffer.DiscountValue / 100), 2)
            : Math.Max(0, product.Price.Value - activeOffer.DiscountValue);
        }

        // 4. Determine Stock Status from InventoryRecord
        // Customer module never writes to InventoryRecord here, just reads status.
        // It relies on RetailerId and ProductId
        var inventory = await _context.InventoryRecords.AsNoTracking()
            .FirstOrDefaultAsync(i => i.ProductId == product.Id, cancellationToken);

        string stockStatus = "Out of Stock";
        if (inventory != null)
        {
            if (inventory.CurrentStock <= 0) stockStatus = "Out of Stock";
            else if (inventory.CurrentStock <= inventory.LowStockThreshold) stockStatus = "Low Stock";
            else stockStatus = "In Stock";
        }

        // 5. Determine IsFavorite
        bool isFavorite = false;
        if (_currentUserService.IsAuthenticated && _currentUserService.CustomerId != Guid.Empty)
        {
            isFavorite = await _context.CustomerFavorites.AsNoTracking()
                .AnyAsync(f => f.CustomerId == _currentUserService.CustomerId && f.ProductId == product.Id, cancellationToken);
        }

        string? brandName = product.Brand;

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

        var images = product.Images.OrderBy(i => i.DisplayOrder).Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.DisplayOrder)).ToList();

        CategoryInfoDto? categoryInfo = null;
        if (product.CategoryId.HasValue)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == product.CategoryId.Value, cancellationToken);

            if (category != null)
            {
                categoryInfo = new CategoryInfoDto(category.Id, category.Name, category.Description);
            }
        }

        // CP-026 handles reviews, so we set AverageRating to null and ReviewCount to 0 for now.
        return new ProductDetailDto(
            product.Id,
            product.Name,
            brandName,
            product.Price,
            discountedPrice,
            product.Description,
            product.Features,
            product.WashInstructions,
            product.ViewsCount, 
            null, // AverageRating
            0, // ReviewCount
            product.AvailableSizes,
            product.AvailableColors,
            stockStatus,
            isFavorite,
            attributes,
            images,
            categoryInfo
        );
    }
}
