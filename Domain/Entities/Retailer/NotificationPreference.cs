namespace Domain.Entities.Retailer;

/// <summary>
/// Per-retailer notification and alert preferences.
/// One record per retailer (1:1 relationship — unique FK on retailer_id).
///
/// Seeded atomically inside the RegisterStep2 transaction so that every
/// active account has a preference record from its first moment of existence.
///
/// All settings default to enabled (true) on creation.
/// </summary>
public sealed class NotificationPreference : BaseEntity
{
    // =========================================================================
    // Fields
    // =========================================================================

    /// <summary>
    /// FK to the owning retailer account.
    /// Unique constraint enforced at DB level (one record per retailer).
    /// </summary>
    public Guid RetailerId { get; private set; }

    /// <summary>
    /// Email/in-app alert when a tracked product falls below its low-stock threshold.
    /// </summary>
    public bool LowStockAlerts { get; private set; }

    /// <summary>
    /// Alert when any order transitions to a new status
    /// (Processing, Shipped, Delivered, Cancelled).
    /// </summary>
    public bool OrderStatusAlerts { get; private set; }

    /// <summary>
    /// Alert when the current subscription is expiring, auto-renewing, or a payment has failed.
    /// </summary>
    public bool SubscriptionAlerts { get; private set; }

    /// <summary>Email delivery channel enabled/disabled for all alert types.</summary>
    public bool EmailNotifications { get; private set; }

    /// <summary>In-app notification panel delivery channel enabled/disabled.</summary>
    public bool InAppNotifications { get; private set; }

    // =========================================================================
    // EF Core Constructor (private)
    // =========================================================================

    private NotificationPreference() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a default NotificationPreference record for a newly activated retailer.
    /// All channels and alert types default to <c>true</c> (opted in).
    ///
    /// This method must be called inside the RegisterStep2 transaction so that
    /// the preference row and the account activation are committed atomically.
    /// </summary>
    /// <param name="retailerId">The ID of the retailer this preference belongs to.</param>
    public static NotificationPreference CreateDefault(Guid retailerId)
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException(
                "RetailerId must not be an empty GUID.", nameof(retailerId));

        return new NotificationPreference
        {
            RetailerId = retailerId,
            LowStockAlerts = true,
            OrderStatusAlerts = true,
            SubscriptionAlerts = true,
            EmailNotifications = true,
            InAppNotifications = true,
        };
    }
}