

namespace Domain.Entities.Orders;

/// <summary>
/// Immutable financial audit record created when a commission is deducted from a
/// retailer's available balance on order delivery.
///
/// DESIGN RULES:
///   • All setters are private — CommissionRecord is write-once (audit log).
///   • Does NOT extend BaseEntity — only id, retailer_id, order_id, amount,
///     commission_rate, and created_at columns exist. No updated_at or soft-delete.
///   • Created exclusively by CommissionDeductionHandler inside the
///     OrderDeliveredEvent dispatch, within the order status update transaction.
/// </summary>
public sealed class CommissionRecord
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>FK to the retailer whose balance was deducted.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>FK to the order that triggered the commission.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Commission amount deducted = Order.TotalAmount × CommissionRate.
    /// Stored as a positive value representing the amount deducted.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// Snapshot of the commission rate applied at the time of deduction.
    /// Stored as a decimal fraction (e.g. 0.0500 = 5%).
    /// </summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>UTC timestamp when the commission was recorded.</summary>
    public DateTime CreatedAt { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private CommissionRecord() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new CommissionRecord.
    /// Called exclusively from <see cref="CommissionDeductionHandler"/>.
    /// </summary>
    /// <param name="retailerId">Retailer whose balance is deducted.</param>
    /// <param name="orderId">Order that triggered the commission.</param>
    /// <param name="orderTotal">Order total amount used to compute the commission.</param>
    /// <param name="commissionRate">Decimal fraction rate (0 to 1).</param>
    public static CommissionRecord Create(
        Guid retailerId,
        Guid orderId,
        decimal orderTotal,
        decimal commissionRate)
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));

        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));

        if (orderTotal < 0)
            throw new ArgumentOutOfRangeException(nameof(orderTotal),
                "Order total cannot be negative.");

        if (commissionRate is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(commissionRate),
                "Commission rate must be between 0 and 1.");

        return new CommissionRecord
        {
            Id = Guid.NewGuid(),
            RetailerId = retailerId,
            OrderId = orderId,
            Amount = Math.Round(orderTotal * commissionRate, 2, MidpointRounding.AwayFromZero),
            CommissionRate = commissionRate,
            CreatedAt = DateTime.UtcNow
        };
    }
}
