namespace Domain.Enums.Subscription;

/// <summary>
/// Represents the lifecycle state of a retailer's subscription.
/// Values match the DB CHECK constraint on subscriptions.status.
/// State machine transitions are enforced in Subscription.cs domain methods.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Retailer has never had a subscription (default state on registration).
    /// </summary>
    None = 0,

    /// <summary>
    /// 14-day free trial is active.
    /// </summary>
    Trial = 1,

    /// <summary>
    /// Paid subscription is currently active.
    /// </summary>
    Active = 2,

    /// <summary>
    /// Subscription is active but scheduled to downgrade at the next renewal date.
    /// </summary>
    PendingDowngrade = 3,

    /// <summary>
    /// Subscription period ended and was not renewed.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Subscription was explicitly cancelled by the retailer or admin.
    /// </summary>
    Cancelled = 5
}