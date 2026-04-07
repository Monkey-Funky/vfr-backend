
namespace Domain.Entities.Analytics;

/// <summary>
/// Append-only event log for real-time activity feed.
/// Written on every significant action (order placed, product created, login, etc.).
/// Indexed on (retailer_id, created_at DESC) for the real-time feed query.
/// NOT soft-deleted — events are immutable audit records.
/// </summary>
public sealed class ActivityEvent
{
    public Guid Id { get; private set; }

    /// <summary>Owning retailer. Every query MUST scope by this column.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>
    /// Discriminator string for the event type.
    /// Examples: 'ProductCreated', 'OrderPlaced', 'LoginSuccess', 'StockAdjusted'.
    /// Stored as varchar(50).
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    /// <summary>Optional FK to the affected resource (product Id, order Id, etc.).</summary>
    public Guid? ResourceId { get; private set; }

    /// <summary>Structured metadata stored as JSONB. Never null; defaults to '{}'.</summary>
    public string EventData { get; private set; } = "{}";

    /// <summary>UTC timestamp of the event. Indexed DESC for recency queries.</summary>
    public DateTime CreatedAt { get; private set; }

    private ActivityEvent() { } // EF Core

    public ActivityEvent(
        Guid retailerId,
        string eventType,
        Guid? resourceId = null,
        string? eventData = null)
    {
        Id = Guid.NewGuid();
        RetailerId = retailerId;
        EventType = eventType;
        ResourceId = resourceId;
        EventData = eventData ?? "{}";
        CreatedAt = DateTime.UtcNow;
    }
}