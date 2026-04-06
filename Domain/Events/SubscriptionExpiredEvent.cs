namespace Domain.Events;

/// <summary>
/// Raised by <c>SubscriptionExpiryJob</c> when a retailer subscription
/// has expired or is imminently expiring.
/// Triggers <see cref="Application.Features.Notifications.Events.SubscriptionExpiryNotificationHandler"/>.
/// </summary>

public sealed record SubscriptionExpiredEvent(
    Guid RetailerId,
    DateTime ExpiresAt,
    DateTime OccurredAt) : IDomainEvent;