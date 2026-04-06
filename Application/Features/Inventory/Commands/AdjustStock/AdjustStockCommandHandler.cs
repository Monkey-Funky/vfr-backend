using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Inventory.Commands.AdjustStock;

/// <summary>
/// Handles AdjustStockCommand — sets inventory stock to an absolute value.
///
/// FLOW:
///   1. Resolve RetailerId from JWT.
///   2. Fetch the InventoryRecord WITH tracking (optimistic lock via RowVersion).
///   3. Validate IDOR — record must belong to the authenticated retailer.
///   4. Call inventoryRecord.AdjustStock(newQuantity, type, reason, adjustedById).
///      Domain method validates newQuantity >= 0 (throws STOCK_FLOOR_VIOLATION on failure).
///   5. Write StockAdjustment audit record.
///   6. If CurrentStock &lt;= LowStockThreshold → publish LowStockWarningEvent.
///   7. SaveChangesAsync — ALL changes are committed in ONE transaction.
///
/// OPTIMISTIC CONCURRENCY:
///   On DbUpdateConcurrencyException:
///     • Attempt 1 fails → re-fetch the record (reload fresh RowVersion) and retry.
///     • Attempt 2 fails → throw BusinessRuleException(code:"CONCURRENT_STOCK_UPDATE").
///   This matches the "retry once, then fail" requirement in the spec.
///
/// CACHE INVALIDATION:
///   After a successful save the entire inventory cache prefix for the retailer
///   is invalidated so that subsequent reads reflect the new stock level.
/// </summary>
public sealed class AdjustStockCommandHandler
    : IRequestHandler<AdjustStockCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AdjustStockCommandHandler> _logger;

    public AdjustStockCommandHandler(
        IUnitOfWork unitOfWork,
        IInventoryRepository inventoryRepository,
        IMediator mediator,
        ICurrentUserService currentUserService,
        ICacheService cacheService,
        ILogger<AdjustStockCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _inventoryRepository = inventoryRepository;
        _mediator = mediator;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        AdjustStockCommand command,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        // ── Two attempts: initial + one retry on concurrency conflict ─────────
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                // Variables declared outside the lambda so they are readable after
                // ExecuteInTransactionAsync returns (for post-transaction side-effects).
                int oldQuantity = 0;
                bool shouldRaiseLowStockAlert = false;
                LowStockWarningEvent? lowStockEvent = null;

                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    // ── Step 1: Load with tracking (RowVersion included in WHERE clause) ──
                    var inventoryRecord = await _inventoryRepository.GetTrackedByIdAsync(
                        retailerId,
                        command.InventoryRecordId,
                        ct)
                        ?? throw new NotFoundException(
                            nameof(InventoryRecord), command.InventoryRecordId);

                    // ── Step 2: IDOR guard ────────────────────────────────────────────────
                    // GetTrackedByIdAsync already scopes by retailerId, but we validate
                    // explicitly as belt-and-suspenders against future repo changes.
                    if (inventoryRecord.RetailerId != retailerId)
                        throw new NotFoundException(
                            nameof(InventoryRecord), command.InventoryRecordId);

                    // ── Step 3: Domain method — validates >= 0, returns old quantity ──────
                    // FIX (Error 1): AdjustStock returns oldQuantity so the caller can
                    // (a) build the audit record without a second DB read and
                    // (b) determine whether the threshold was just crossed.
                    oldQuantity = inventoryRecord.AdjustStock(
                        newQuantity: command.NewQuantity,
                        type: command.Type,
                        reason: command.Reason,
                        adjustedById: retailerId);

                    // ── Step 4: Audit record (same transaction) ───────────────────────────
                    var adjustment = StockAdjustment.Create(
                        inventoryRecordId: inventoryRecord.Id,
                        adjustmentType: command.Type,
                        oldQuantity: oldQuantity,
                        newQuantity: command.NewQuantity,
                        adjustedById: retailerId,
                        reason: command.Reason);

                    await _unitOfWork.Repository<StockAdjustment>()
                        .AddAsync(adjustment, ct);

                    // ── Step 5: Commit — emits UPDATE + INSERT in one DB transaction ──────
                    // DbUpdateConcurrencyException is thrown here if RowVersion changed.
                    await _unitOfWork.SaveChangesAsync(ct);

                    // ── Step 6: BUG C FIX — threshold-crossing check ──────────────────────
                    // Raise LowStockWarningEvent ONLY when crossing from above to at/below.
                    // NOT every time stock is adjusted while already below the threshold.
                    //
                    // oldQuantity > LowStockThreshold  → was above threshold before adjustment
                    // CurrentStock <= LowStockThreshold → is now at or below threshold
                    //
                    // If stock was already below threshold and just got lower, no new alert.
                    // This prevents notification spam on repeated decrements.
                    bool wasAbove = oldQuantity > inventoryRecord.LowStockThreshold;
                    bool isNowAtOrBelow = inventoryRecord.CurrentStock <= inventoryRecord.LowStockThreshold;

                    if (wasAbove && isNowAtOrBelow)
                    {
                        shouldRaiseLowStockAlert = true;

                        // FIX (Error 1): Parameter renamed LowStockThreshold (not Threshold)
                        // to match InventoryRecord.LowStockThreshold and LowStockWarningEvent record.
                        lowStockEvent = new LowStockWarningEvent(
                            RetailerId: inventoryRecord.RetailerId,
                            ProductId: inventoryRecord.ProductId,
                            ProductName: inventoryRecord.ProductName,
                            CurrentStock: inventoryRecord.CurrentStock,
                            LowStockThreshold: inventoryRecord.LowStockThreshold);
                    }

                    // ── Step 7: Publish event inside transaction scope ────────────────────
                    // LowStockWarningEventHandler.Handle() will call _unitOfWork.SaveChangesAsync
                    // for the Notification entity — that second save participates in the same
                    // open DB transaction (same DbContext scope).
                    if (shouldRaiseLowStockAlert && lowStockEvent is not null)
                    {
                        await _mediator.Publish(lowStockEvent, ct);
                    }

                }, cancellationToken);

                // ── Step 8: Cache invalidation (after transaction commits) ──────────────
                await _cacheService.RemoveByPrefixAsync(
                    $"inventory:{retailerId:N}:", cancellationToken);

                return Result<bool>.Success(true, "Stock adjusted successfully.");
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                // ── Single retry on concurrency conflict ──────────────────────────────
                // Re-entering the loop re-fetches the entity with the latest RowVersion.
                // The absolute NewQuantity is re-applied against the fresh stock level.
                _logger.LogWarning(
                    "Concurrency conflict adjusting InventoryRecord {Id} (attempt 1/2). Retrying...",
                    command.InventoryRecordId);

                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                // Falls through to attempt 2
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // ── Both attempts exhausted → fail with domain error ──────────────────
                _logger.LogError(ex,
                    "Concurrency conflict adjusting InventoryRecord {Id} — both attempts exhausted.",
                    command.InventoryRecordId);

                throw new BusinessRuleException(
                    "CONCURRENT_STOCK_UPDATE",
                    "The inventory record was modified by another request at the same time. " +
                    "Please retrieve the latest stock level and try again.");
            }
        }

        // Unreachable — the loop always returns or throws above
        return Result<bool>.Failure("Unexpected error in AdjustStockCommandHandler.");
    }
}