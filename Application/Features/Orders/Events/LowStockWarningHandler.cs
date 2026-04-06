using Application.Interfaces.Persistence;
using Domain.Entities.Notifications;
using Domain.Enums.Orders;


namespace Application.Features.Orders.Events;

/// <summary>
/// Creates an in-app low-stock notification when any order item's inventory
/// drops to or below the LowStockThreshold after shipment.
///
/// FIX: Notification now extends BaseEntity → IUnitOfWork.Repository{Notification}() is valid.
///
/// TRANSACTION: Runs INSIDE the same ExecuteInTransactionAsync block.
/// If this handler throws, the whole transaction (status + inventory + commission) rolls back.
/// </summary>
public sealed class LowStockWarningHandler
    : INotificationHandler<OrderStatusChangedEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LowStockWarningHandler> _logger;

    public LowStockWarningHandler(
        IUnitOfWork unitOfWork,
        ILogger<LowStockWarningHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(
        OrderStatusChangedEvent notification,
        CancellationToken cancellationToken)
    {
        // Low-stock check only applies when order moves to Shipped
        if (notification.NewStatus != OrderStatus.Shipped)
            return;

        var inventoryRepo = _unitOfWork.Repository<InventoryRecord>();
        var notificationRepo = _unitOfWork.Repository<Notification>(); // ✅ valid — extends BaseEntity

        foreach (var item in notification.Items)
        {
            if (item.ProductId is null)
                continue;

            var inventory = await inventoryRepo.FirstOrDefaultAsync(
                ir => ir.RetailerId == notification.RetailerId
                   && ir.ProductId == item.ProductId
                   && !ir.IsDeleted,
                cancellationToken);

            if (inventory is null)
                continue;

            // Only create a notification if stock is at or below the threshold
            if (inventory.CurrentStock > inventory.LowStockThreshold)
                continue;

            var stockNotification = Notification.Create(
                retailerId: notification.RetailerId,
                type: Notification.NotificationType.LowStock,
                title: "Low Stock Alert",
                body: $"Product '{inventory.ProductName}' has only " +
                            $"{inventory.CurrentStock} unit(s) remaining " +
                            $"(threshold: {inventory.LowStockThreshold}).",
                resourceId: item.ProductId);

            await notificationRepo.AddAsync(stockNotification, cancellationToken); // ✅

            _logger.LogInformation(
                "Low-stock notification created for Product {ProductId} — Stock: {Stock}/{Threshold}",
                item.ProductId, inventory.CurrentStock, inventory.LowStockThreshold);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}