using Application.Features.Notifications.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;

namespace Application.Features.Notifications.Events;

/// <summary>
/// Handles <see cref="SubscriptionExpiredEvent"/> raised by <c>SubscriptionExpiryJob</c>.
/// Creates a Notification(Type = SubscriptionExpiring) for the retailer.
///
/// FAILURE ISOLATION: Any exception is caught and logged — the expiry job
/// run is NOT aborted due to a notification failure.
/// </summary>
public sealed class SubscriptionExpiryNotificationHandler
    : INotificationHandler<SubscriptionExpiredEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationHub _notificationHub;
    private readonly ILogger<SubscriptionExpiryNotificationHandler> _logger;

    public SubscriptionExpiryNotificationHandler(
        IUnitOfWork unitOfWork,
        INotificationHub notificationHub,
        ILogger<SubscriptionExpiryNotificationHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationHub = notificationHub;
        _logger = logger;
    }

    public async Task Handle(
        SubscriptionExpiredEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            Notification subscriptionNotification = Notification.Create(
                retailerId: notification.RetailerId,
                type: Notification.NotificationType.SubscriptionExpiring,
                title: "Subscription Expiring",
                body: $"Your subscription expired on {notification.ExpiresAt:dd MMM yyyy}. " +
                            "Please renew to continue using VFR services.",
                resourceId: null);

            await _unitOfWork.Repository<Notification>().AddAsync(subscriptionNotification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _ = _notificationHub.SendNotificationAsync(
                notification.RetailerId.ToString(),
                subscriptionNotification.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SubscriptionExpiryNotificationHandler failed for Retailer {RetailerId}. " +
                "Business state is NOT affected.",
                notification.RetailerId);
        }
    }
}