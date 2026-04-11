using Application.Features.Products.DTOs;
using Application.Features.Products.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Products.Commands.CreateProduct;


/// <summary>
/// Handles product creation with:
///   1. Plan limit enforcement (concurrency-safe — check inside transaction)
///   2. Category/SubCategory ownership validation
///   3. Barcode uniqueness per retailer
///   4. S3 image upload BEFORE the transaction
///   5. Atomic Product + InventoryRecord creation in ONE transaction
/// </summary>
public sealed class CreateProductCommandHandler
    : IRequestHandler<CreateProductCommand, Result<ProductDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductRepository _productRepo;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public CreateProductCommandHandler(
        IUnitOfWork unitOfWork,
        IProductRepository productRepo,
        ISubscriptionService subscriptionService,
        IFileStorageService fileStorage,
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _productRepo = productRepo;
        _subscriptionService = subscriptionService;
        _fileStorage = fileStorage;
        _currentUserService = currentUserService;
        _context = context;
        _cache = cache;
    }

    public async Task<Result<ProductDetailDto>> Handle(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // ── STEP 1: Validate Category ownership ──────────────────────────────
        if (command.CategoryId.HasValue)
        {
            var categoryExists = await _context.Categories
                .AnyAsync(
                    c => c.Id == command.CategoryId.Value
                      && c.RetailerId == retailerId
                      && !c.IsDeleted,
                    cancellationToken);

            if (!categoryExists)
                throw new NotFoundException(nameof(Category), command.CategoryId.Value);
        }

        // ── STEP 2: Validate SubCategory belongs to the specified CategoryId ─
        if (command.SubCategoryId.HasValue)
        {
            if (!command.CategoryId.HasValue)
                throw new BusinessRuleException(
                    "SUB_CATEGORY_WITHOUT_CATEGORY",
                    "SubCategoryId cannot be set without a CategoryId.");

            var subCategoryValid = await _context.SubCategories
                .AnyAsync(
                    s => s.Id == command.SubCategoryId.Value
                      && s.CategoryId == command.CategoryId.Value
                      && s.RetailerId == retailerId
                      && !s.IsDeleted,
                    cancellationToken);

            if (!subCategoryValid)
                throw new NotFoundException(nameof(SubCategory), command.SubCategoryId.Value);
        }

        // ── STEP 3: Validate barcode uniqueness within the retailer's scope ──
        // Duplicate barcode across DIFFERENT retailers is allowed.
        if (!string.IsNullOrWhiteSpace(command.Barcode))
        {
            var barcodeExists = await _productRepo.GetByBarcodeAsync(
                retailerId, command.Barcode, cancellationToken);

            if (barcodeExists is not null)
                throw new ConflictException(
                    nameof(Product),
                    nameof(command.Barcode),
                    command.Barcode);
        }

        // ── STEP 4: Upload images to S3 BEFORE the transaction ───────────────
        var uploadedImageUrls = new List<(string Url, int Order)>();

        if (command.Images is { Length: > 0 })
        {
            for (var i = 0; i < command.Images.Length; i++)
            {
                var file = command.Images[i];

                await using var stream = file.Content;

                var url = await _fileStorage.UploadAsync(
                    stream: stream,
                    fileName: file.FileName,
                    folder: $"products/{retailerId}",
                    ct: cancellationToken);

                uploadedImageUrls.Add((url, i));
            }
        }

        // ── STEP 5: Plan limit check + atomic Product + InventoryRecord ──────
        Product? createdProduct = null;

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // ── 5a. Plan limit check (inside transaction — TOCTOU-safe) ───────
            var planInfo = await _subscriptionService.GetCurrentPlanAsync(retailerId, ct);

            if (planInfo?.MaxActiveProducts.HasValue == true)
            {
                var activeCount = await _productRepo
                    .GetActiveProductCountByRetailerAsync(retailerId, ct);

                if (activeCount >= planInfo.MaxActiveProducts.Value)
                    throw new BusinessRuleException(
                        "PRODUCT_LIMIT_EXCEEDED",
                        $"Your plan allows up to {planInfo.MaxActiveProducts.Value} active products.");
            }

            // ── 5b. Create the Product aggregate ─────────────────────────────
            createdProduct = Product.Create(
                retailerId: retailerId,
                name: command.Name,
                description: command.Description,
                categoryId: command.CategoryId,
                subCategoryId: command.SubCategoryId,
                price: command.Price,
                currency: command.Currency,
                barcode: command.Barcode,
                status: command.Status);

            await _unitOfWork.Repository<Product>().AddAsync(createdProduct, ct);

            // ── 5c. Create InventoryRecord atomically ─────────────────────────
            var inventoryRecord = InventoryRecord.Create(
                retailerId: retailerId,
                productId: createdProduct.Id,
                productName: createdProduct.Name,
                initialQuantity: command.InitialQuantity,
                lowStockThreshold: 10);

            await _unitOfWork.Repository<InventoryRecord>().AddAsync(inventoryRecord, ct);

            // ── 5d. Attach uploaded images ────────────────────────────────────
            foreach (var (url, order) in uploadedImageUrls)
                createdProduct.AddImage(url, order);

            // ── 5e. Single SaveChanges for all entities ───────────────────────
            await _unitOfWork.SaveChangesAsync(ct);

        }, cancellationToken);

        // ── STEP 6: Invalidate Redis cache ────────────────────────────────────
        await _cache.RemoveAsync(
            CacheKeys.ActiveProductCount(retailerId),
            cancellationToken);

        // ── Build response DTO ────────────────────────────────────────────────
        string? categoryName = null;
        string? subCategoryName = null;

        if (createdProduct!.CategoryId.HasValue)
        {
            categoryName = await _context.Categories
                .AsNoTracking()
                .Where(c => c.Id == createdProduct.CategoryId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (createdProduct.SubCategoryId.HasValue)
        {
            subCategoryName = await _context.SubCategories
                .AsNoTracking()
                .Where(s => s.Id == createdProduct.SubCategoryId.Value)
                .Select(s => s.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var dto = createdProduct.ToDetailDto(
            categoryName: categoryName,
            subCategoryName: subCategoryName,
            currentStock: command.InitialQuantity,
            inventoryStatus: InventoryStatus.Derive(command.InitialQuantity, 10));

        return Result<ProductDetailDto>.Success(dto, "Product created successfully.");
    }
}