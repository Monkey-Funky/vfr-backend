using Application.Features.Products.DTOs;
using Application.Features.Products.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetProductByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<ProductDetailDto> Handle(
        GetProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        string cacheKey = $"product:{retailerId:N}:{query.ProductId:N}";

        var cached = await _cacheService.GetAsync<ProductDetailDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        // Fetch product + images in one query
        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(
                p => p.Id == query.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Product), query.ProductId);

        // Parallelise the two independent lookups
        var categoryTask = product.CategoryId.HasValue
            ? _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == product.CategoryId.Value && !c.IsDeleted)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : Task.FromResult<string?>(null);

        var subCategoryTask = product.SubCategoryId.HasValue
            ? _context.SubCategories
                .AsNoTracking()
                .Where(s => s.Id == product.SubCategoryId.Value && !s.IsDeleted)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : Task.FromResult<string?>(null);

        var inventoryTask = _context.InventoryRecords
            .AsNoTracking()
            .Where(i => i.ProductId == product.Id && !i.IsDeleted)
            .Select(i => new { i.CurrentStock, i.Status })
            .FirstOrDefaultAsync(cancellationToken);

        await Task.WhenAll(categoryTask, subCategoryTask, inventoryTask);

        var categoryName = await categoryTask;
        var subCategoryName = await subCategoryTask;
        var inventory = await inventoryTask;

        var dto = product.ToDetailDto(
            categoryName: categoryName,
            subCategoryName: subCategoryName,
            currentStock: inventory?.CurrentStock,
            inventoryStatus: inventory?.Status);

        // Cache for 10 minutes; invalidated on Update/Delete/ToggleStatus commands.
        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);

        return dto;
    }
}
