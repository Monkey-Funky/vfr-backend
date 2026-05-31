
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Products.Commands.ToggleProductStatus;

public sealed class ToggleProductStatusCommandHandler
    : IRequestHandler<ToggleProductStatusCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public ToggleProductStatusCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IApplicationDbContext context,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _context = context;
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

        // ── OutOfStock → Active guard ────────────────────────────────────────
        // When transitioning from OutOfStock, verify the product actually has
        // stock before allowing activation. Prevents phantom "active" products
        // that would immediately appear out-of-stock in the catalogue.
        if (product.Status == ProductStatus.OutOfStock)
        {
            int currentStock = await _context.InventoryRecords
                .AsNoTracking()
                .Where(ir => ir.ProductId == command.ProductId
                          && ir.RetailerId == retailerId
                          && !ir.IsDeleted)
                .Select(ir => ir.CurrentStock)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentStock <= 0)
                throw new BusinessRuleException(
                    "INSUFFICIENT_STOCK",
                    "Cannot activate a product with zero stock. " +
                    "Please adjust the inventory before activating.");
        }

        var newStatus = product.ToggleStatus();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate active product count cache after status change.
        await Task.WhenAll(
            _cache.RemoveAsync(CacheKeys.ActiveProductCount(retailerId), cancellationToken),
            _cache.RemoveAsync(CacheKeys.ProductDetail(retailerId, command.ProductId), cancellationToken),
            _cache.RemoveByPrefixAsync(CacheKeys.ProductListPrefix(retailerId), cancellationToken),
            _cache.RemoveByPrefixAsync("catalog:browse:", cancellationToken)
        );

        return Result<string>.Success(newStatus, $"Product status changed to {newStatus}.");
    }
}