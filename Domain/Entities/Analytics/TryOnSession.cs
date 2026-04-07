
namespace Domain.Entities.Analytics;

/// <summary>
/// Records each virtual try-on session initiated by a customer.
/// Used to compute conversion rate (sessions that led to purchase / total sessions).
/// NOT soft-deleted — sessions are immutable analytics records.
/// </summary>
public sealed class TryOnSession
{
    public Guid Id { get; private set; }
    public Guid RetailerId { get; private set; }

    /// <summary>Product that was virtually tried on. Nullable: product may be deleted.</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Customer who initiated the session (cross-module FK).</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Duration in seconds.</summary>
    public int SessionDurationSeconds { get; private set; }

    /// <summary>True if the session led to an order placement.</summary>
    public bool ResultedInPurchase { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private TryOnSession() { } // EF Core

    public TryOnSession(
        Guid retailerId,
        Guid? productId,
        Guid? customerId,
        int sessionDurationSeconds,
        bool resultedInPurchase)
    {
        Id = Guid.NewGuid();
        RetailerId = retailerId;
        ProductId = productId;
        CustomerId = customerId;
        SessionDurationSeconds = sessionDurationSeconds;
        ResultedInPurchase = resultedInPurchase;
        CreatedAt = DateTime.UtcNow;
    }
}