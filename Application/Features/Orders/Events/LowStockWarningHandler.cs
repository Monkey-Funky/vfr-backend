using Application.Interfaces.Persistence;
using Domain.Entities.Notifications;
using Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;


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
    private static readonly TimeSpan DedupWindow = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LowStockWarningHandler> _logger;

    public LowStockWarningHandler(
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        ILogger<LowStockWarningHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _context = context;
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
        var notificationRepo = _unitOfWork.Repository<Notification>();

        DateTime dedupCutoff = DateTime.UtcNow.Subtract(DedupWindow);

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

            // FIX OR-3: 24-hour dedup guard — skip if a LowStock notification
            // for this product was already created within the dedup window.
            bool alreadyNotified = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(
                    n => n.RetailerId == notification.RetailerId
                      && n.Type == Notification.NotificationType.LowStock
                      && n.ResourceId == item.ProductId
                      && n.CreatedAt >= dedupCutoff,
                    cancellationToken);

            if (alreadyNotified)
            {
                _logger.LogDebug(
                    "LowStockWarningHandler: Skipping duplicate notification for " +
                    "Product {ProductId} (already created within {Window}h).",
                    item.ProductId, DedupWindow.TotalHours);
                continue;
            }

            var stockNotification = Notification.Create(
                retailerId: notification.RetailerId,
                type: Notification.NotificationType.LowStock,
                title: "Low Stock Alert",
                body: $"Product '{inventory.ProductName}' has only " +
                            $"{inventory.CurrentStock} unit(s) remaining " +
                            $"(threshold: {inventory.LowStockThreshold}).",
                resourceId: item.ProductId);

            await notificationRepo.AddAsync(stockNotification, cancellationToken);

            _logger.LogInformation(
                "Low-stock notification created for Product {ProductId} — Stock: {Stock}/{Threshold}",
                item.ProductId, inventory.CurrentStock, inventory.LowStockThreshold);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}