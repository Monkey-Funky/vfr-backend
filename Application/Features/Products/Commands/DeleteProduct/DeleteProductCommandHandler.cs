using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Products.Commands.DeleteProduct;

public sealed class DeleteProductCommandHandler
    : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public DeleteProductCommandHandler(
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

    public async Task<Result> Handle(
        DeleteProductCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // IDOR guard — verify ownership before any mutation.
        var productExists = await _context.Products
            .AnyAsync(
                p => p.Id == command.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken);

        if (!productExists)
            throw new NotFoundException(nameof(Product), command.ProductId);

        // All soft-deletes and offer inactivation in ONE transaction.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // ── 1. Soft-delete the Product ────────────────────────────────────
            var trackedProduct = await _unitOfWork.GetTrackedByIdAsync<Product>(
                command.ProductId, ct)
                ?? throw new NotFoundException(nameof(Product), command.ProductId);

            trackedProduct.SoftDelete();

            // ── 2. Soft-delete all child ProductImage records ─────────────────
            // Load WITHOUT AsNoTracking so EF Core tracks changes.
            var images = await _context.ProductImages
                .Where(i => i.ProductId == command.ProductId && !i.IsDeleted)
                .ToListAsync(ct);

            foreach (var image in images)
                image.SoftDelete();

            // ── 3. Soft-delete the associated InventoryRecord ─────────────────
            var inventoryRecord = await _context.InventoryRecords
                .FirstOrDefaultAsync(
                    ir => ir.ProductId == command.ProductId
                       && ir.RetailerId == retailerId
                       && !ir.IsDeleted,
                    ct);

            inventoryRecord?.SoftDelete();

            // ── 4. Inactivate any active Offers referencing this product ───────
            // Cannot have an active Offer for a deleted product.
            // Uses ExecuteUpdateAsync — bulk SQL UPDATE inside the open transaction.
            await _context.Offers
                .Where(o => o.ProductId == command.ProductId
                         && o.Status == "Active"
                         && !o.IsDeleted)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(o => o.Status, "Inactive")
                        .SetProperty(o => o.UpdatedAt, DateTime.UtcNow),
                    ct);

            // ── 5. Single SaveChanges for product + images + inventory ─────────
            // (Offer update was already flushed by ExecuteUpdateAsync above.)
            await _unitOfWork.SaveChangesAsync(ct);

        }, cancellationToken);

        // ── 6. Invalidate Redis cache ─────────────────────────────────────────
        await _cache.RemoveAsync(
            CacheKeys.ActiveProductCount(retailerId),
            cancellationToken);

        return Result.Success("Product deleted successfully.");
    }
}