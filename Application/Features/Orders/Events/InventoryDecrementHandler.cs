using Application.Interfaces.Persistence;
using Domain.Enums.Orders;

namespace Application.Features.Orders.Events;

/// <summary>
/// Decrements inventory stock when an order status transitions to 'Shipped'.
/// Creates an immutable StockAdjustment audit record for each order item.
///
/// FIX: StockAdjustment now extends BaseEntity → IUnitOfWork.Repository{StockAdjustment}() is valid.
///
/// TRANSACTION: Runs INSIDE the same ExecuteInTransactionAsync block as UpdateOrderStatusCommandHandler.
/// If this handler throws, the entire transaction rolls back (status change + all other handlers).
/// </summary>
public sealed class InventoryDecrementHandler
    : INotificationHandler<OrderStatusChangedEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryDecrementHandler> _logger;

    public InventoryDecrementHandler(
        IUnitOfWork unitOfWork,
        ILogger<InventoryDecrementHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(
        OrderStatusChangedEvent notification,
        CancellationToken cancellationToken)
    {
        // Only decrement inventory when the order moves to Shipped
        if (notification.NewStatus != OrderStatus.Shipped)
            return;

        var inventoryRepo = _unitOfWork.Repository<InventoryRecord>();
        var adjustmentRepo = _unitOfWork.Repository<StockAdjustment>(); // ✅ valid — extends BaseEntity

        foreach (var item in notification.Items)
        {
            if (item.ProductId is null)
                continue; // product was deleted — no inventory record to decrement

            var inventory = await inventoryRepo.FirstOrDefaultAsync(
                ir => ir.RetailerId == notification.RetailerId
                   && ir.ProductId == item.ProductId
                   && !ir.IsDeleted,
                cancellationToken);

            if (inventory is null)
            {
                _logger.LogWarning(
                    "InventoryDecrementHandler: No inventory record found for Product {ProductId} " +
                    "in Retailer {RetailerId}. Skipping.",
                    item.ProductId, notification.RetailerId);
                continue;
            }

            int oldQty = inventory.CurrentStock;

            // AdjustStock domain method enforces current_stock >= 0
            inventory.AdjustStock(
                delta: -item.Quantity,
                adjustmentType: AdjustmentType.OrderSale,
                adjustedById: notification.RetailerId);

            // Persist the updated inventory record
            await inventoryRepo.UpdateAsync(inventory, cancellationToken);

            // Create immutable audit record — ✅ now valid (StockAdjustment : BaseEntity)
            var adjustment = StockAdjustment.Create(
                inventoryRecordId: inventory.Id,
                adjustmentType: AdjustmentType.OrderSale,
                oldQuantity: oldQty,
                newQuantity: inventory.CurrentStock,
                adjustedById: notification.RetailerId,
                reason: $"Order {notification.OrderId} — {item.Quantity} unit(s) shipped.");

            await adjustmentRepo.AddAsync(adjustment, cancellationToken);
        }

        // Save all inventory + adjustment changes inside the transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
