using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.UpdateProduct;


public sealed class UpdateProductCommandHandler
    : IRequestHandler<UpdateProductCommand, Result<ProductDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public UpdateProductCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _context = context;
    }

    public async Task<Result<ProductDetailDto>> Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // ── STEP 1: Load product with IDOR guard ──────────────────────────────
        var product = await _unitOfWork.Repository<Product>()
            .FirstOrDefaultAsync(
                p => p.Id == command.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.ProductId);

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

        // ── STEP 3: Validate new SubCategoryId belongs to the new CategoryId ──
        if (command.ShouldUpdateCategory && command.NewSubCategoryId.HasValue)
        {
            var resolvedCategoryId = command.NewCategoryId ?? product.CategoryId;

            if (!resolvedCategoryId.HasValue)
                throw new BusinessRuleException(
                    "SUB_CATEGORY_WITHOUT_CATEGORY",
                    "SubCategoryId cannot be set without a CategoryId.");

            var subCategoryValid = await _context.SubCategories
                .AnyAsync(
                    s => s.Id == command.NewSubCategoryId.Value
                      && s.CategoryId == resolvedCategoryId.Value
                      && s.RetailerId == retailerId
                      && !s.IsDeleted,
                    cancellationToken);

            if (!subCategoryValid)
                throw new NotFoundException(nameof(SubCategory), command.NewSubCategoryId.Value);
        }

        // ── STEP 4: Validate barcode uniqueness (if changed) ──────────────────
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

        // ── STEP 5: Apply domain update ───────────────────────────────────────
        // We re-load with tracking so EF Core can detect changes.
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

        // ── STEP 6: Sync InventoryRecord product name snapshot if name changed ─
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