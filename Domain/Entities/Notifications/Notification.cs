
namespace Domain.Entities.Notifications;

/// <summary>
/// Represents an in-app notification delivered to a retailer.
/// Use the <see cref="Create"/> factory method — the parameterless constructor
/// is reserved for EF Core materialisation only.
/// </summary>
public sealed class Notification : BaseEntity
{
    public static class NotificationType
    {
        public const string LowStock = "LowStock";
        public const string OrderStatusChanged = "OrderStatusChanged";
        public const string SubscriptionExpiring = "SubscriptionExpiring";
        public const string PaymentFailed = "PaymentFailed";
        public const string AccountDeletion = "AccountDeletion";
    }

    public Guid RetailerId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTime? ReadAt { get; private set; }
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
    /// notification is a no-op.
    /// </summary>
    public void MarkAsRead()
    {
        if (IsRead)
            return;

        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}