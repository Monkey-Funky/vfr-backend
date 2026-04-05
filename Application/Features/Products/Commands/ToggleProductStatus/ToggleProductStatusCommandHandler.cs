
namespace Application.Features.Products.Commands.ToggleProductStatus;


public sealed class ToggleProductStatusCommandHandler
    : IRequestHandler<ToggleProductStatusCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ToggleProductStatusCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<string>> Handle(
        ToggleProductStatusCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        var product = await _unitOfWork.GetTrackedByIdAsync<Product>(
            command.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.ProductId);

        // IDOR guard
        if (product.RetailerId != retailerId)
            throw new UnauthorizedException(
                "You do not have permission to access this resource.");

        if (product.IsDeleted)
            throw new NotFoundException(nameof(Product), command.ProductId);

        var newStatus = product.ToggleStatus();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<string>.Success(newStatus, $"Product status changed to {newStatus}.");
    }
}