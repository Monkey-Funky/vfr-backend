
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Shared.Constants;

namespace Application.Features.Products.Commands.ToggleProductStatus;

public sealed class ToggleProductStatusCommandHandler
    : IRequestHandler<ToggleProductStatusCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cache;

    public ToggleProductStatusCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _cache = cache;
    }

    public async Task<Result<string>> Handle(
        ToggleProductStatusCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // Load with tracking so EF Core detects the status change.
        var product = await _unitOfWork.GetTrackedByIdAsync<Product>(
            command.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.ProductId);

        // IDOR guard
        if (product.RetailerId != retailerId)
            throw new UnauthorizedException(
                "You do not have permission to access this resource.");

        if (product.IsDeleted)
            throw new NotFoundException(nameof(Product), command.ProductId);

        // TODO: When transitioning from OutOfStock → Active, verify that
        //       InventoryRecord.CurrentStock > 0 for this product. This requires
        //       checking IApplicationDbContext.InventoryRecords inside this handler.
        //       Inject IApplicationDbContext and add:
        //         var stock = await _context.InventoryRecords
        //             .Where(ir => ir.ProductId == command.ProductId && !ir.IsDeleted)
        //             .Select(ir => ir.CurrentStock)
        //             .FirstOrDefaultAsync(cancellationToken);
        //         if (product.Status == ProductStatus.OutOfStock && stock <= 0)
        //             throw new BusinessRuleException("INSUFFICIENT_STOCK",
        //                 "Cannot activate a product with zero stock.");

        var newStatus = product.ToggleStatus();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate active product count cache after status change.
        await _cache.RemoveAsync(
            CacheKeys.ActiveProductCount(retailerId),
            cancellationToken);

        return Result<string>.Success(newStatus, $"Product status changed to {newStatus}.");
    }
}