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
    private const int MaxAttempts = 2; // initial attempt + 1 retry

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

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    // ── Load WITH tracking so RowVersion participates in UPDATE WHERE ──
                    var inventoryRecord = await _inventoryRepository.GetTrackedByIdAsync(
                        retailerId,
                        command.InventoryRecordId,
                        ct)
                        ?? throw new NotFoundException(
                            nameof(InventoryRecord), command.InventoryRecordId);

                    // IDOR check (belt-and-suspenders — repository already scopes by retailerId)
                    if (inventoryRecord.RetailerId != retailerId)
                        throw new NotFoundException(
                            nameof(InventoryRecord), command.InventoryRecordId);

                    // ── Domain call: validates >= 0, returns old quantity ─────────────
                    int oldQuantity = inventoryRecord.AdjustStock(
                        newQuantity: command.NewQuantity,
                        type: command.Type,
                        reason: command.Reason,
                        adjustedById: retailerId);

                    // ── Audit record in the same transaction ─────────────────────────
                    var adjustment = StockAdjustment.Create(
                        inventoryRecordId: inventoryRecord.Id,
                        adjustmentType: command.Type,
                        oldQuantity: oldQuantity,
                        newQuantity: command.NewQuantity,
                        adjustedById: retailerId,
                        reason: command.Reason);

                    await _unitOfWork.Repository<StockAdjustment>()
                        .AddAsync(adjustment, ct);

                    // ── Single SaveChangesAsync — triggers optimistic concurrency check ─
                    await _unitOfWork.SaveChangesAsync(ct);

                    // ── Low-stock event (published inside transaction scope) ───────────
                    if (inventoryRecord.CurrentStock <= inventoryRecord.LowStockThreshold)
                    {
                        await _mediator.Publish(new LowStockWarningEvent(
                            RetailerId: inventoryRecord.RetailerId,
                            ProductId: inventoryRecord.ProductId,
                            ProductName: inventoryRecord.ProductName,
                            CurrentStock: inventoryRecord.CurrentStock,
                            LowStockThreshold: inventoryRecord.LowStockThreshold), ct);
                    }

                }, cancellationToken);

                // ── Cache invalidation (only on success) ─────────────────────────────
                await _cacheService.RemoveByPrefixAsync(
                    $"inventory:{retailerId:N}:", cancellationToken);

                return Result<bool>.Success(true, "Stock adjusted successfully.");
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    "Concurrency conflict adjusting stock for InventoryRecord {Id} " +
                    "(attempt {Attempt}/{Max}). Retrying...",
                    command.InventoryRecordId, attempt, MaxAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex,
                    "Concurrency conflict adjusting stock for InventoryRecord {Id} — " +
                    "all {Max} attempts exhausted.",
                    command.InventoryRecordId, MaxAttempts);

                // FIX: ConflictException(string message) — single-argument constructor
                throw new ConflictException(
                    "The inventory record was modified by another request. " +
                    "Please retrieve the latest stock level and try again.");
            }
        }

        // Unreachable — loop always returns or throws
        return Result<bool>.Failure("Unexpected error in AdjustStockCommandHandler.");
    }
}