using Application.Features.Products.DTOs;
using Application.Features.Products.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandHandler
    : IRequestHandler<UpdateProductCommand, Result<ProductDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public UpdateProductCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _context = context;
        _cache = cache;
    }

    public async Task<Result<ProductDetailDto>> Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // ── STEP 1: IDOR guard — confirm ownership ────────────────────────────
        var productExists = await _context.Products
            .AnyAsync(
                p => p.Id == command.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken);

        if (!productExists)
            throw new NotFoundException(nameof(Product), command.ProductId);

        // ── STEP 2: Validate new CategoryId ownership ─────────────────────────
        if (command.ShouldUpdateCategory && command.NewCategoryId.HasValue)
        {
            var categoryExists = await _context.Categories
                .AnyAsync(
                    c => c.Id == command.NewCategoryId.Value
                      && c.RetailerId == retailerId
                      && !c.IsDeleted,
                    cancellationToken);

            if (!categoryExists)
                throw new NotFoundException(nameof(Category), command.NewCategoryId.Value);
        }

        // ── STEP 3: Validate SubCategoryId belongs to the (resolved) CategoryId ─
        if (command.ShouldUpdateCategory && command.NewSubCategoryId.HasValue)
        {
            // Resolve the effective categoryId: prefer the incoming value,
            // fall back to the product's current value (loaded below via tracked load).
            // We use a separate count query to avoid loading the full entity here.
            var effectiveCategoryId = command.NewCategoryId;

            if (!effectiveCategoryId.HasValue)
            {
                effectiveCategoryId = await _context.Products
                    .AsNoTracking()
                    .Where(p => p.Id == command.ProductId)
                    .Select(p => p.CategoryId)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (!effectiveCategoryId.HasValue)
                throw new BusinessRuleException(
                    "SUB_CATEGORY_WITHOUT_CATEGORY",
                    "SubCategoryId cannot be set without a CategoryId.");

            var subCategoryValid = await _context.SubCategories
                .AnyAsync(
                    s => s.Id == command.NewSubCategoryId.Value
                      && s.CategoryId == effectiveCategoryId.Value
                      && s.RetailerId == retailerId
                      && !s.IsDeleted,
                    cancellationToken);

            if (!subCategoryValid)
                throw new NotFoundException(nameof(SubCategory), command.NewSubCategoryId.Value);
        }

        // ── STEP 4: Barcode uniqueness — exclude current product ──────────────
        // WHERE barcode = @b AND retailer_id = @r AND id != @id AND is_deleted = false
        if (command.ShouldUpdateBarcode && !string.IsNullOrWhiteSpace(command.NewBarcode))
        {
            var barcodeConflict = await _context.Products
                .AnyAsync(
                    p => p.Barcode == command.NewBarcode
                      && p.RetailerId == retailerId
                      && p.Id != command.ProductId
                      && !p.IsDeleted,
                    cancellationToken);

            if (barcodeConflict)
                throw new ConflictException(
                    nameof(Product),
                    nameof(command.NewBarcode),
                    command.NewBarcode);
        }

        // ── STEP 5: Load tracked entity and apply domain update ───────────────
        var trackedProduct = await _unitOfWork.GetTrackedByIdAsync<Product>(
            command.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.ProductId);

        trackedProduct.Update(
            newName: command.NewName,
            newDescription: command.NewDescription,
            shouldUpdateDescription: command.ShouldUpdateDescription,
            newPrice: command.NewPrice,
            shouldUpdatePrice: command.ShouldUpdatePrice,
            newBarcode: command.NewBarcode,
            shouldUpdateBarcode: command.ShouldUpdateBarcode,
            newCategoryId: command.NewCategoryId,
            shouldUpdateCategory: command.ShouldUpdateCategory,
            newSubCategoryId: command.NewSubCategoryId,
            newStatus: command.NewStatus);

        // ── STEP 6: Sync InventoryRecord product name snapshot ────────────────
        if (command.NewName is not null)
        {
            var inventoryRecord = await _context.InventoryRecords
                .FirstOrDefaultAsync(
                    ir => ir.ProductId == trackedProduct.Id
                       && ir.RetailerId == retailerId
                       && !ir.IsDeleted,
                    cancellationToken);

            inventoryRecord?.UpdateProductName(trackedProduct.Name);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── STEP 7: Invalidate Redis cache ────────────────────────────────────
        await Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.ActiveProductCount(retailerId), cancellationToken),
            _cache.RemoveAsync(CacheKeys.ProductDetail(retailerId, trackedProduct.Id), cancellationToken),
            _cache.RemoveByPrefixAsync(CacheKeys.ProductListPrefix(retailerId), cancellationToken),
            _cache.RemoveByPrefixAsync("catalog:browse:", cancellationToken)
        );

        // ── Build response DTO ────────────────────────────────────────────────
        string? categoryName = null;
        string? subCategoryName = null;

        if (trackedProduct.CategoryId.HasValue)
        {
            categoryName = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == trackedProduct.CategoryId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (trackedProduct.SubCategoryId.HasValue)
        {
            subCategoryName = await _context.SubCategories
                .AsNoTracking()
                .Where(s => s.Id == trackedProduct.SubCategoryId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var dto = trackedProduct.ToDetailDto(
            categoryName: categoryName,
            subCategoryName: subCategoryName);

        return Result<ProductDetailDto>.Success(dto, "Product updated successfully.");
    }
}