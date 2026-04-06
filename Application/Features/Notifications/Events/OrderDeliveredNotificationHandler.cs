using Application.Features.Notifications.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;

namespace Application.Features.Notifications.Events;

/// <summary>
/// Handles <see cref="OrderDeliveredEvent"/>.
/// Creates a Notification(Type = OrderStatusChanged) for the retailer.
///
/// FAILURE ISOLATION: Any exception is caught and logged. The business state
/// (order delivered, commission deducted, inventory decremented) is NEVER
/// rolled back due to a notification failure.
/// </summary>
public sealed class OrderDeliveredNotificationHandler
    : INotificationHandler<OrderDeliveredEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationHub _notificationHub;
    private readonly ILogger<OrderDeliveredNotificationHandler> _logger;

    public OrderDeliveredNotificationHandler(
        IUnitOfWork unitOfWork,
        INotificationHub notificationHub,
        ILogger<OrderDeliveredNotificationHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationHub = notificationHub;
        _logger = logger;
    }

    public async Task Handle(
        OrderDeliveredEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // FIX: OrderDeliveredEvent has OrderId (Guid), not OrderReference (string).
            //      Use the short form of the Guid as a human-readable reference.
            string orderRef = notification.OrderId.ToString()[..8].ToUpperInvariant();

            Notification orderNotification = Notification.Create(
                retailerId: notification.RetailerId,
                type: Notification.NotificationType.OrderStatusChanged,
                title: "Order Delivered",
                body: $"Order #{orderRef} has been delivered successfully.",
                resourceId: notification.OrderId);

            await _unitOfWork.Repository<Notification>().AddAsync(orderNotification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _ = _notificationHub.SendNotificationAsync(
                notification.RetailerId.ToString(),
                orderNotification.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "OrderDeliveredNotificationHandler failed for Order {OrderId} " +
                "(Retailer {RetailerId}). Business state is NOT affected.",
                notification.OrderId, notification.RetailerId);
        }
    }
}