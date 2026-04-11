using Application.Interfaces.Persistence;
using Application.Interfaces.Services;

namespace Application.Features.Inventory.Commands.SetLowStockThreshold;

/// <summary>
/// Handles SetLowStockThresholdCommand — updates the LowStockThreshold on an inventory record.
///
/// IDOR protection:
///   GetTrackedByIdAsync scopes the lookup to both InventoryRecordId AND RetailerId.
///   If the record belongs to another retailer, NotFoundException is returned.
///
/// Cache invalidation:
///   Clears all inventory cache entries for the retailer after a successful save.
/// </summary>
public sealed class SetLowStockThresholdCommandHandler
    : IRequestHandler<SetLowStockThresholdCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<SetLowStockThresholdCommandHandler> _logger;

    public SetLowStockThresholdCommandHandler(
        IUnitOfWork unitOfWork,
        IInventoryRepository inventoryRepository,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<SetLowStockThresholdCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _inventoryRepository = inventoryRepository;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        SetLowStockThresholdCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Load WITH tracking ───────────────────────────────────────────────
        var record = await _inventoryRepository.GetTrackedByIdAsync(
            retailerId,
            command.InventoryRecordId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryRecord), command.InventoryRecordId);

        // IDOR check (belt-and-suspenders)
        if (record.RetailerId != retailerId)
            throw new NotFoundException(nameof(InventoryRecord), command.InventoryRecordId);

        // ── Domain method validates range [0, 10 000] ────────────────────────
        record.SetLowStockThreshold(command.NewThreshold);

        // ── Single SaveChangesAsync ──────────────────────────────────────────
        await _unitOfWork.Repository<InventoryRecord>().UpdateAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ── Cache invalidation ───────────────────────────────────────────────
        await _cacheService.RemoveByPrefixAsync(
            $"inventory:{retailerId:N}:", cancellationToken);

        _logger.LogInformation(
            "SetLowStockThreshold — updated to {Threshold} for InventoryRecord {Id}. " +
            "RetailerId: {RetailerId}",
            command.NewThreshold, command.InventoryRecordId, retailerId);

        return Result<bool>.Success(true, "Low stock threshold updated successfully.");
    }
}
