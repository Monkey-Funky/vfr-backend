namespace Domain.Entities.Retailer;

/// <summary>
/// Immutable financial record of a platform commission deducted from a retailer
/// upon successful order delivery.
///
/// WHY IT EXISTS:
///   IApplicationDbContext.CommissionRecords DbSet is declared in the baseline contract
///   (InitialCode.md). This entity provides the concrete type for that DbSet.
///
/// DESIGN:
///   • All setters are private — records are immutable after creation.
///   • Created inside CommissionDeductionHandler when an order transitions to Delivered.
///   • CommissionRate is captured at delivery time — NOT re-read later if the plan changes.
///   • UpdatedAt, CreatedBy, UpdatedBy are ignored in EF config (not in DB schema).
///   • IsDeleted is ignored — commission records are never soft-deleted (financial audit trail).
/// </summary>


/// <summary>
/// Immutable financial record of a platform commission deducted from a retailer
/// upon successful order delivery.
///
/// WHY IT EXISTS:
///   IApplicationDbContext.CommissionRecords DbSet is declared in the baseline contract
///   (InitialCode.md). This entity provides the concrete type for that DbSet.
///
/// DESIGN:
///   • All setters are private — records are immutable after creation.
///   • Created inside CommissionDeductionHandler when an order transitions to Delivered.
///   • CommissionRate is captured at delivery time — NOT re-read later if the plan changes.
///   • UpdatedAt, CreatedBy, UpdatedBy are ignored in EF config (not in DB schema).
///   • IsDeleted is ignored — commission records are never soft-deleted (financial audit trail).
/// </summary>
public sealed class CommissionRecord : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the retailer charged this commission.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>FK to the order that triggered this commission.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>FK to the subscription plan active at the time of delivery.</summary>
    public Guid SubscriptionPlanId { get; private set; }

    /// <summary>
    /// The commission rate applied at the moment of order delivery.
    /// Captured as a snapshot — immune to future plan changes.
    /// </summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>The full order total on which the commission was calculated.</summary>
    public decimal OrderTotal { get; private set; }

    /// <summary>
    /// The actual commission amount charged: OrderTotal × CommissionRate,
    /// rounded to 2 decimal places.
    /// </summary>
    public decimal CommissionAmount { get; private set; }

    /// <summary>ISO 4217 currency code (e.g. "EGP").</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Timestamp of the Delivered status transition that triggered this record.</summary>
    public DateTime DeliveredAt { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private CommissionRecord() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates an immutable commission record.
    /// Called from CommissionDeductionHandler after order status transitions to Delivered.
    /// </summary>
    /// <param name="retailerId">The retailer charged.</param>
    /// <param name="orderId">The delivered order.</param>
    /// <param name="subscriptionPlanId">Plan ID active at delivery time.</param>
    /// <param name="commissionRate">Rate captured at delivery time (e.g. 0.05 for 5%).</param>
    /// <param name="orderTotal">Full order total amount.</param>
    /// <param name="currency">ISO currency code.</param>
    /// <param name="deliveredAt">UTC timestamp of delivery.</param>
    public static CommissionRecord Create(
        Guid retailerId,
        Guid orderId,
        Guid subscriptionPlanId,
        decimal commissionRate,
        decimal orderTotal,
        string currency,
        DateTime deliveredAt)
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));
        if (subscriptionPlanId == Guid.Empty)
            throw new ArgumentException("SubscriptionPlanId must not be empty.", nameof(subscriptionPlanId));
        if (commissionRate < 0)
            throw new ArgumentException("CommissionRate must be non-negative.", nameof(commissionRate));
        if (orderTotal < 0)
            throw new ArgumentException("OrderTotal must be non-negative.", nameof(orderTotal));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency, nameof(currency));

        return new CommissionRecord
        {
            Id = Guid.NewGuid(),
            RetailerId = retailerId,
            OrderId = orderId,
            SubscriptionPlanId = subscriptionPlanId,
            CommissionRate = commissionRate,
            OrderTotal = orderTotal,
            CommissionAmount = Math.Round(orderTotal * commissionRate, 2),
            Currency = currency,
            DeliveredAt = deliveredAt,
        };
    }
}