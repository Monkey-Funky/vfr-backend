using Application.Interfaces.Persistence;
using Application.Interfaces.Services;


namespace Application.Features.Inventory.Commands.DeleteInventoryRecord;
/// <summary>
/// Handles DeleteInventoryRecordCommand — soft-deletes the inventory record.
///
/// IDOR protection:
///   The record is fetched with BOTH InventoryRecordId AND RetailerId — if the ID
///   belongs to a different retailer, NotFoundException is returned (not ForbiddenException)
///   to avoid confirming the resource's existence to an unauthorised caller.
///
/// Soft delete:
///   Sets IsDeleted = true via InventoryRecord.SoftDelete(). The parent Product is untouched.
///   EF Core global query filter (HasQueryFilter(r => !r.IsDeleted)) ensures the record
///   disappears from all subsequent inventory queries automatically.
/// </summary>
public sealed class DeleteInventoryRecordCommandHandler
    : IRequestHandler<DeleteInventoryRecordCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public DeleteInventoryRecordCommandHandler(
        IUnitOfWork unitOfWork,
        IInventoryRepository inventoryRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _unitOfWork = unitOfWork;
        _inventoryRepository = inventoryRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<Result<bool>> Handle(
        DeleteInventoryRecordCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        var record = await _inventoryRepository.GetTrackedByIdAsync(
            retailerId,
            command.InventoryRecordId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryRecord), command.InventoryRecordId);

        // IDOR guard (belt-and-suspenders — repository already scopes by retailerId)
        if (record.RetailerId != retailerId)
            throw new NotFoundException(nameof(InventoryRecord), command.InventoryRecordId);

        // Domain method — sets IsDeleted = true, UpdatedAt = UtcNow
        record.SoftDelete();

        await _unitOfWork.Repository<InventoryRecord>().UpdateAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidate all inventory cache entries for this retailer
        await _cacheService.RemoveByPrefixAsync(
            $"inventory:{retailerId:N}:", cancellationToken);

        return Result<bool>.Success(true, "Inventory record deleted successfully.");
    }
}