using Application.Features.Notifications.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;


namespace Application.Features.Notifications.Events;

/// <summary>
/// Handles <see cref="PaymentFailedEvent"/>.
/// Creates a Notification(Type = PaymentFailed) for the retailer.
///
/// FAILURE ISOLATION: Any exception is caught and logged — the payment
/// processing flow is NEVER rolled back due to a notification failure.
/// </summary>
public sealed class PaymentFailedNotificationHandler
    : INotificationHandler<PaymentFailedEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationHub _notificationHub;
    private readonly ILogger<PaymentFailedNotificationHandler> _logger;

    public PaymentFailedNotificationHandler(
        IUnitOfWork unitOfWork,
        INotificationHub notificationHub,
        ILogger<PaymentFailedNotificationHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _notificationHub = notificationHub;
        _logger = logger;
    }

    public async Task Handle(
        PaymentFailedEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            Notification paymentNotification = Notification.Create(
                retailerId: notification.RetailerId,
                type: Notification.NotificationType.PaymentFailed,
                title: "Payment Failed",
                body: $"Your recent payment could not be processed. " +
                            $"Reason: {notification.FailureReason}. " +
                            "Please update your payment details.",
                resourceId: null);

            await _unitOfWork.Repository<Notification>().AddAsync(paymentNotification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _ = _notificationHub.SendNotificationAsync(
                notification.RetailerId.ToString(),
                paymentNotification.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PaymentFailedNotificationHandler failed for Retailer {RetailerId}. " +
                "Business state is NOT affected.",
                notification.RetailerId);
        }
    }
}