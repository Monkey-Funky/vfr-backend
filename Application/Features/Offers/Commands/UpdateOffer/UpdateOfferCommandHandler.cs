using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Offer;

namespace Application.Features.Offers.Commands.UpdateOffer;

public sealed class UpdateOfferCommandHandler
    : IRequestHandler<UpdateOfferCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICacheService _cacheService;

    public UpdateOfferCommandHandler(
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

    public async Task<Result<bool>> Handle(
        UpdateOfferCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // IDOR guard: retailerId in predicate — mismatched retailer receives 404
        Offer offer = await _unitOfWork
            .Repository<Offer>()
            .FirstOrDefaultAsync(
                o => o.Id == command.OfferId && o.RetailerId == retailerId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Offer), command.OfferId);

        // ── Upload new cover image if supplied ─────────────────────────────────
        // null CoverImage = keep existing image; skip upload entirely.
        string? newCoverImageUrl = null;

        if (command.CoverImage is not null)
        {
            string folder = $"retailers/{retailerId:N}/offers";

            // "await using" disposes the stream after upload completes.
            // The stream was pre-opened by the controller (IFormFile.OpenReadStream()).
            await using var stream = command.CoverImage.Content;

            newCoverImageUrl = await _fileStorageService.UploadAsync(
                stream: stream,
                fileName: command.CoverImage.FileName,   // extension extraction only; GUID generated inside FileStorageService
                folder: folder,
                ct: cancellationToken);
        }

        // ── Validate fixed discount ceiling against current product price ───────
        // Only applies to Product-type offers with a Fixed discount.
        // Skipped if ProductId is null (product was deleted — graceful degradation).
        if (offer.OfferType == OfferType.Product &&
            command.DiscountType == DiscountType.Fixed &&
            offer.ProductId.HasValue)
        {
            await ValidateFixedDiscountCeilingAsync(
                command, offer.ProductId.Value, cancellationToken);
        }

        // ── Delegate mutation to the domain method ─────────────────────────────
        offer.Update(
            title: command.Title,
            description: command.Description,
            discountType: command.DiscountType,
            discountValue: command.DiscountValue,
            startDate: command.StartDate,
            endDate: command.EndDate,
            status: command.Status,
            newCoverImageUrl: newCoverImageUrl);
            
        await _unitOfWork.Repository<Offer>().UpdateAsync(offer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── Invalidate list and single-entity caches ───────────────────────────
        await _cacheService.RemoveByPrefixAsync($"offers:{retailerId}:", cancellationToken);

        return Result<bool>.Success(true);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Checks that a Fixed-type discount does not exceed the product's current price.
    /// Called only for Product-type offers where ProductId is still populated.
    /// If the product has been soft-deleted and is no longer findable, the check is
    /// skipped (graceful degradation — the offer retains its historical value).
    /// </summary>
    private async Task ValidateFixedDiscountCeilingAsync(
        UpdateOfferCommand command,
        Guid productId,
        CancellationToken cancellationToken)
    {
        Product? product = await _unitOfWork
            .Repository<Product>()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product?.Price is not null && command.DiscountValue > product.Price)
            throw new BusinessRuleException(
                "DISCOUNT_EXCEEDS_PRICE",
                $"Fixed discount value ({command.DiscountValue:F2}) cannot exceed " +
                $"the product price ({product.Price:F2}).");
    }
}