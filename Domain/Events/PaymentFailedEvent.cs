namespace Domain.Events;

/// <summary>
/// Raised when a recurring payment attempt fails.
/// Triggers <see cref="Application.Features.Notifications.Events.PaymentFailedNotificationHandler"/>.
/// </summary>

public sealed record PaymentFailedEvent(
    Guid RetailerId,
    string FailureReason,
    DateTime OccurredAt) : IDomainEvent;