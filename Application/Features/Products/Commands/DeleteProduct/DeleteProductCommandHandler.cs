using Microsoft.EntityFrameworkCore;

namespace Application.Features.Products.Commands.DeleteProduct;


public sealed class DeleteProductCommandHandler
    : IRequestHandler<DeleteProductCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApplicationDbContext _context;

    public DeleteProductCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IApplicationDbContext context)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _context = context;
    }

    public async Task<Result> Handle(
        DeleteProductCommand command,
        CancellationToken cancellationToken)
    {
        var retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity claim is missing.");

        // Verify product exists and belongs to this retailer (IDOR guard)
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.Id == command.ProductId
                  && p.RetailerId == retailerId
                  && !p.IsDeleted,
                cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.ProductId);

        // Soft-delete product AND inventory record atomically in one transaction
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var trackedProduct = await _unitOfWork.GetTrackedByIdAsync<Product>(
                command.ProductId, ct)
                ?? throw new NotFoundException(nameof(Product), command.ProductId);

            await _unitOfWork.Repository<Product>().SoftDeleteAsync(trackedProduct, ct);

            // Soft-delete the associated InventoryRecord in the same transaction
            var inventoryRecord = await _context.InventoryRecords
                .FirstOrDefaultAsync(
                    ir => ir.ProductId == command.ProductId
                       && ir.RetailerId == retailerId
                       && !ir.IsDeleted,
                    ct);

            if (inventoryRecord is not null)
                await _unitOfWork.Repository<InventoryRecord>()
                    .SoftDeleteAsync(inventoryRecord, ct);

            await _unitOfWork.SaveChangesAsync(ct);

        }, cancellationToken);

        return Result.Success("Product deleted successfully.");
    }
}
