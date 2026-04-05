namespace Application.Features.Products.Commands.AddProductImage;

public sealed class AddProductImageCommandHandler
    : IRequestHandler<AddProductImageCommand, Result<ProductImageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentUserService _currentUserService;

    public AddProductImageCommandHandler(
        IUnitOfWork unitOfWork,
        IFileStorageService fileStorage,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _fileStorage = fileStorage;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ProductImageDto>> Handle(
        AddProductImageCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // Load with tracking so we can mutate the Images collection
        var product = await _unitOfWork.GetTrackedByIdAsync<Product>(
            command.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.ProductId);

        // IDOR guard
        if (product.RetailerId != retailerId)
            throw new UnauthorizedException(
                "You do not have permission to access this resource.");

        if (product.IsDeleted)
            throw new NotFoundException(nameof(Product), command.ProductId);

        await using var stream = command.ImageFile.Content;

        var imageUrl = await _fileStorage.UploadAsync(
            stream: stream,
            fileName: command.ImageFile.FileName,   // extension extraction only; GUID generated inside FileStorageService
            folder: $"products/{retailerId}",
            ct: cancellationToken);

        // Add image via domain method (validates URL, creates ProductImage entity)
        product.AddImage(imageUrl, command.DisplayOrder);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Return the last-added image DTO
        var addedImage = product.Images
            .Where(i => i.ImageUrl == imageUrl && !i.IsDeleted)
            .OrderByDescending(i => i.DisplayOrder)
            .First();

        return Result<ProductImageDto>.Success(
            addedImage.ToDto(),
            "Image added successfully.");
    }
}