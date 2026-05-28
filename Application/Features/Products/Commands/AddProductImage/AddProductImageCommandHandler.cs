using Application.Features.Products.DTOs;
using Application.Features.Products.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.AddProductImage;

public sealed class AddProductImageCommandHandler
    : IRequestHandler<AddProductImageCommand, Result<ProductImageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public AddProductImageCommandHandler(
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

    public async Task<Result<ProductImageDto>> Handle(
        AddProductImageCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // Load the product WITH its Images collection so EF Core tracks the collection
        // and detects the addition of the new ProductImage during SaveChangesAsync.
        // Using IApplicationDbContext directly (same pattern as RemoveProductImageCommandHandler)
        // because the generic GetTrackedByIdAsync<T> does not support Include expressions.
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == command.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.ProductId);

        // IDOR guard — the product must belong to the authenticated retailer.
        if (product.RetailerId != retailerId)
            throw new UnauthorizedException(
                "You do not have permission to access this resource.");

        // Explicit soft-delete guard (complements the global query filter).
        if (product.IsDeleted)
            throw new NotFoundException(nameof(Product), command.ProductId);

        await using var stream = command.ImageFile.Content;

        var imageUrl = await _fileStorage.UploadAsync(
            stream: stream,
            fileName: command.ImageFile.FileName,
            folder: $"products/{retailerId}",
            ct: cancellationToken);

        // Add image via domain method — validates URL, creates ProductImage entity,
        // and appends it to the tracked Images collection so EF detects the change.
        var addedImage = product.AddImage(imageUrl, command.DisplayOrder);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ProductImageDto>.Success(
            addedImage.ToDto(),
            "Image added successfully.");
    }
}