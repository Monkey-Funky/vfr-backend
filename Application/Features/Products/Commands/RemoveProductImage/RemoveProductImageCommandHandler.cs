using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.RemoveProductImage;

public sealed class RemoveProductImageCommandHandler
    : IRequestHandler<RemoveProductImageCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public RemoveProductImageCommandHandler(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _currentUserService = currentUserService;
        _context = context;
    }

    public async Task<Result> Handle(
        RemoveProductImageCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // IDOR guard — verify the product belongs to this retailer.
        var productExists = await _context.Products
            .AnyAsync(
                p => p.Id == command.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken);

        if (!productExists)
            throw new NotFoundException(nameof(Product), command.ProductId);

        // Load the image record (not soft-deleted).
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(
                i => i.Id == command.ImageId
                  && i.ProductId == command.ProductId
                  && !i.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(ProductImage), command.ImageId);

        var imageUrl = image.ImageUrl;

        // Soft-delete DB record first — DB is the source of truth.
        // If S3 delete fails later, the image is already hidden from all responses.
        image.SoftDelete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Best-effort S3 delete. Failure is logged but does NOT roll back the DB change.
        // A scheduled cleanup job reconciles any orphaned S3 objects.
        try
        {
            await _fileStorage.DeleteAsync(imageUrl, cancellationToken);
        }
        catch (ExternalServiceException)
        {
            // Non-fatal: image is already soft-deleted in DB.
        }

        return Result.Success("Image removed successfully.");
    }
}