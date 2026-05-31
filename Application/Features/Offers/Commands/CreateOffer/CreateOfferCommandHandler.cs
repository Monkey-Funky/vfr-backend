
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;
using Domain.Enums.Product;

namespace Application.Features.Offers.Commands.CreateOffer;


public sealed class CreateOfferCommandHandler
    : IRequestHandler<CreateOfferCommand, Result<Guid>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;

    public CreateOfferCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _cacheService = cacheService;
    }

    public async Task<Result<Guid>> Handle(
        CreateOfferCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Step 1: Validate target entity ownership ───────────────────────────
        await ValidateTargetEntityAsync(command, retailerId, cancellationToken);

        // ── Step 2: Validate fixed-type discount ceiling against product price ─
        decimal? productPrice = null;

        if (command.OfferType == OfferType.Product && command.ProductId.HasValue)
            productPrice = await GetProductPriceAsync(command.ProductId.Value, cancellationToken);

        ValidateDiscountValue(command, productPrice);

        // ── Step 3: Upload cover image ─────────────────────────────────────────
        string coverImageUrl = await UploadCoverImageAsync(
            command, retailerId, cancellationToken);

        // ── Step 4: Create and persist the offer entity ────────────────────────
        Offer offer = Offer.Create(
            retailerId: retailerId,
            title: command.Title,
            description: command.Description,
            offerType: command.OfferType,
            productId: command.ProductId,
            categoryId: command.CategoryId,
            discountType: command.DiscountType,
            discountValue: command.DiscountValue,
            startDate: command.StartDate,
            endDate: command.EndDate,
            coverImageUrl: coverImageUrl);

        await _unitOfWork.Repository<Offer>().AddAsync(offer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── Step 5: Invalidate cache ───────────────────────────────────────────
        await Task.WhenAll(
            _cacheService.RemoveByPrefixAsync($"offers:{retailerId}:", cancellationToken),
            _cacheService.RemoveByPrefixAsync("catalog:offers:", cancellationToken),
            _cacheService.RemoveByPrefixAsync("catalog:browse:", cancellationToken)
        );

        return Result<Guid>.Success(offer.Id);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the target entity (Product or Category) exists AND belongs
    /// to this retailer. Throws NotFoundException for mismatches — IDOR-safe
    /// (attacker cannot distinguish "not found" from "not yours").
    /// </summary>
    private async Task ValidateTargetEntityAsync(
        CreateOfferCommand command,
        Guid retailerId,
        CancellationToken cancellationToken)
    {
        if (command.OfferType == OfferType.Product)
        {
            // Product must exist, belong to this retailer, and be Active
            Product? product = await _unitOfWork
                .Repository<Product>()
                .FirstOrDefaultAsync(
                    p => p.Id == command.ProductId!.Value &&
                         p.RetailerId == retailerId,
                    cancellationToken);

            if (product is null)
                throw new NotFoundException(nameof(Product), command.ProductId!.Value);

            if (product.Status != ProductStatus.Active)
                throw new BusinessRuleException(
                    "PRODUCT_NOT_ACTIVE",
                    "Offers can only be created for Active products. " +
                    "Activate the product before creating an offer.");
        }
        else // Category
        {
            bool categoryExists = await _unitOfWork
                .Repository<Category>()
                .AnyAsync(
                    c => c.Id == command.CategoryId!.Value &&
                         c.RetailerId == retailerId,
                    cancellationToken);

            if (!categoryExists)
                throw new NotFoundException(nameof(Category), command.CategoryId!.Value);
        }
    }

    /// <summary>
    /// Fetches the product's current price for the Fixed discount ceiling check.
    /// Returns null if the product cannot be found.
    /// </summary>
    private async Task<decimal?> GetProductPriceAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        Product? product = await _unitOfWork
            .Repository<Product>()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        return product?.Price;
    }

    /// <summary>
    /// Enforces that a Fixed-type discount on a product offer does not exceed
    /// the product's price. Percentage-type range is already enforced by the validator.
    /// </summary>
    private static void ValidateDiscountValue(CreateOfferCommand command, decimal? productPrice)
    {
        if (command.DiscountType == DiscountType.Fixed &&
            command.OfferType == OfferType.Product &&
            productPrice.HasValue)
        {
            if (command.DiscountValue > productPrice.Value)
                throw new BusinessRuleException(
                    "DISCOUNT_EXCEEDS_PRICE",
                    $"Fixed discount value ({command.DiscountValue:F2}) cannot exceed " +
                    $"the product price ({productPrice.Value:F2}).");
        }
    }

    /// <summary>
    /// Opens the pre-built FileUploadDto stream and uploads it via IFileStorageService.
    /// The stream is disposed automatically after upload via "await using".
    /// </summary>
    private async Task<string> UploadCoverImageAsync(
        CreateOfferCommand command,
        Guid retailerId,
        CancellationToken cancellationToken)
    {
        string folder = $"retailers/{retailerId:N}/offers";

        // "await using" disposes the stream after IFileStorageService.UploadAsync completes.
        // The stream was pre-opened by the controller (IFormFile.OpenReadStream()).
        await using var stream = command.CoverImage.Content;

        string coverImageUrl = await _fileStorageService.UploadAsync(
            stream: stream,
            fileName: command.CoverImage.FileName,   // extension extraction only; GUID generated inside FileStorageService
            folder: folder,
            ct: cancellationToken);

        return coverImageUrl;
    }
}
