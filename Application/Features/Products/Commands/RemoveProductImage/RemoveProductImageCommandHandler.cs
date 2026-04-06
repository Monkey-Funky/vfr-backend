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

        // Verify product ownership (IDOR guard)
        var productExists = await _context.Products
            .AnyAsync(
                p => p.Id == command.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken);

        if (!productExists)
            throw new NotFoundException(nameof(Product), command.ProductId);

        // Load the image record (not soft-deleted)
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(
                i => i.Id == command.ImageId
                  && i.ProductId == command.ProductId
                  && !i.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(ProductImage), command.ImageId);

        var imageUrl = image.ImageUrl;

        // Soft-delete the DB record first (safe — if S3 delete fails, the image
        // URL is still orphaned but the DB is consistent)
        image.SoftDelete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Delete from S3 (best-effort — failure is logged but not re-thrown)
        try
        {
            await _fileStorage.DeleteAsync(imageUrl, cancellationToken);
        }
        catch (ExternalServiceException)
        {
            // S3 deletion failure is non-fatal. The image is already soft-deleted
            // in the DB. A scheduled cleanup job can reconcile orphaned S3 objects.
        }

        return Result.Success("Image removed successfully.");
    }
}