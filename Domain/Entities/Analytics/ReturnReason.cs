using Domain.Enums.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Analytics;
/// <summary>
/// Records the reason a customer returned an order item.
/// Used to compute return-reason distribution charts and return-rate-by-product analytics.
/// NOT soft-deleted — return records are immutable audit data.
/// </summary>
public sealed class ReturnReason
{
    public Guid Id { get; private set; }

    /// <summary>Owning retailer. Every analytics query MUST scope by this column.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>FK to the returned order item. Nullable if the item has since been deleted.</summary>
    public Guid? OrderItemId { get; private set; }

    /// <summary>FK to the product being returned (denormalized for analytics joins).</summary>
    public Guid? ProductId { get; private set; }

    /// <summary>Reason category selected by the customer at return time.</summary>
    public ReturnReasonType Reason { get; private set; }

    public DateTime ReturnedAt { get; private set; }

    private ReturnReason() { } // EF Core

    public ReturnReason(
        Guid retailerId,
        Guid? orderItemId,
        Guid? productId,
        ReturnReasonType reason)
    {
        Id = Guid.NewGuid();
        RetailerId = retailerId;
        OrderItemId = orderItemId;
        ProductId = productId;
        Reason = reason;
        ReturnedAt = DateTime.UtcNow;
    }
}