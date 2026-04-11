
namespace Domain.Entities.Notifications;

/// <summary>
/// In-app notification for a retailer. Extends BaseEntity for IUnitOfWork.Repository compatibility.
/// IsRead defaults false. ReadAt is set only when MarkAsRead() is called.
/// Composite index (retailer_id, created_at DESC) configured in NotificationConfiguration.
/// </summary>
public sealed class Notification : BaseEntity
{
    public static class NotificationType
    {
        public const string LowStock = "LowStock";
        public const string NewOrder = "NewOrder";
        public const string OrderStatusChanged = "OrderStatusChanged";
        public const string SubscriptionExpiring = "SubscriptionExpiring";
        public const string PaymentFailed = "PaymentFailed";
        public const string SystemAlert = "SystemAlert";
    }

    /// <summary>FK to the owning retailer. Never null after construction.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>Notification type — one of NotificationType constants.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>Short display title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Full notification body text (may contain HTML).</summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>Whether the retailer has read this notification. Defaults false.</summary>
    public bool IsRead { get; private set; }

    /// <summary>UTC timestamp when MarkAsRead() was called. Null until read.</summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>Optional FK to the entity that triggered this notification (product, order, etc.).</summary>
    public Guid? ResourceId { get; private set; }

    // EF Core materialisation only.
    private Notification() { }

    /// <summary>
    /// Creates a new unread notification for the given retailer.
    /// </summary>
    public static Notification Create(
        Guid retailerId,
        string type,
        string title,
        string body,
        Guid? resourceId = null)
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));

        ArgumentException.ThrowIfNullOrWhiteSpace(type, nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        ArgumentException.ThrowIfNullOrWhiteSpace(body, nameof(body));

        return new Notification
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            RetailerId = retailerId,
            Type = type,
            Title = title,
            Body = body,
            IsRead = false,
            ReadAt = null,
            ResourceId = resourceId
        };
    }

    /// <summary>
    /// Marks this notification as read. Idempotent — calling on an already-read
    /// notification is a no-op (no second SaveChanges triggered).
    /// Sets ReadAt = UtcNow.
    /// </summary>
    public void MarkAsRead()
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}