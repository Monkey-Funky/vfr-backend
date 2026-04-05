using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Queries.GetProductById;


public sealed class GetProductByIdQueryHandler
    : IRequestHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetProductByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ProductDetailDto> Handle(
        GetProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        var product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .FirstOrDefaultAsync(
                p => p.Id == query.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Product), query.ProductId);

        // Load category / sub-category names separately to avoid a JOIN on every call
        string? categoryName = null;
        string? subCategoryName = null;

        if (product.CategoryId.HasValue)
        {
            categoryName = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == product.CategoryId.Value && !c.IsDeleted)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (product.SubCategoryId.HasValue)
        {
            subCategoryName = await _context.SubCategories
                .AsNoTracking()
                .Where(s => s.Id == product.SubCategoryId.Value && !s.IsDeleted)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Load inventory summary
        var inventory = await _context.InventoryRecords
            .AsNoTracking()
            .Where(i => i.ProductId == product.Id && !i.IsDeleted)
            .Select(i => new { i.CurrentStock, i.Status })
            .FirstOrDefaultAsync(cancellationToken);

        return product.ToDetailDto(
            categoryName: categoryName,
            subCategoryName: subCategoryName,
            currentStock: inventory?.CurrentStock,
            inventoryStatus: inventory?.Status);
    }
}