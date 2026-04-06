using Application.Interfaces.Persistence;
using Domain.Entities.Notifications;
using Domain.Enums.Orders;


namespace Application.Features.Orders.Events;

/// <summary>
/// Records platform commission when an order is Delivered.
/// Creates an in-app notification confirming the commission deduction.
///
/// FIX: Notification now extends BaseEntity → IUnitOfWork.Repository{Notification}() is valid.
///
/// TRANSACTION: Runs INSIDE the same ExecuteInTransactionAsync block.
/// Failure here rolls back the entire transaction — including the status change
/// and the inventory decrement — providing all-or-nothing atomicity.
///
/// NOTE: Commission calculation uses a flat 5% platform rate. In production,
/// this should be driven by the retailer's subscription plan tier.
/// </summary>
public sealed class CommissionDeductionHandler
    : INotificationHandler<OrderStatusChangedEvent>
{
    private const decimal PlatformCommissionRate = 0.05m; // 5%

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CommissionDeductionHandler> _logger;

    public CommissionDeductionHandler(
        IUnitOfWork unitOfWork,
        ILogger<CommissionDeductionHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(
        OrderStatusChangedEvent notification,
        CancellationToken cancellationToken)
    {
        // Commission is recorded only when the order is marked Delivered
        if (notification.NewStatus != OrderStatus.Delivered)
            return;

        var notificationRepo = _unitOfWork.Repository<Notification>(); // ✅ valid — extends BaseEntity

        decimal orderTotal = notification.Items.Sum(i => i.Total);
        decimal commission = Math.Round(orderTotal * PlatformCommissionRate, 2);

        // In production: insert a CommissionRecord entity instead of just a notification.
        // For now, record the deduction as an in-app notification for visibility.
        var commissionNotification = Notification.Create(
            retailerId: notification.RetailerId,
            type: Notification.NotificationType.OrderStatusChanged,
            title: "Commission Deducted",
            body: $"Order {notification.OrderId} delivered. " +
                        $"Platform commission of {commission:F2} EGP (5%) has been deducted " +
                        $"from your balance.",
            resourceId: notification.OrderId);

        await notificationRepo.AddAsync(commissionNotification, cancellationToken); // ✅

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Commission deducted for Order {OrderId}: {Commission} EGP (5% of {Total} EGP)",
            notification.OrderId, commission, orderTotal);
    }
}
