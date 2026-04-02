namespace Domain.Enums;

/// <summary>
/// Lifecycle state of a subscription payment record.
/// Values match the DB CHECK constraint on subscription_payments.status.
/// </summary>
public enum SubscriptionPaymentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4
}