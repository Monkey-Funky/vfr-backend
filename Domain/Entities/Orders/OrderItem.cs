using Domain.Events;

namespace Domain.Entities.Orders;

/// <summary>
/// A single line item within an Order.
///
/// IMMUTABILITY RULES:
///   • UnitPrice, ProductName, Quantity, and Total all have private setters.
///   • These are snapshot values captured AT ORDER TIME and must never change,
///     even if the underlying Product price or name is later updated.
///   • There is deliberately no public setter for UnitPrice — this is the defence
///     against EF Core change-tracker fixup accidentally overwriting snapshot data.
///   • The Total column is stored (not computed by the DB) to avoid re-calculation
///     on every read. It equals UnitPrice * Quantity and is set once in Create().
///
/// DB NOTES:
///   • Does NOT have updated_at, created_by, updated_by, or is_deleted columns.
///     OrderItemConfiguration explicitly ignores those BaseEntity members.
///   • product_id is nullable (ON DELETE SET NULL) — the snapshot remains even
///     if the product is later deleted from the catalogue.
/// </summary>
public sealed class OrderItem : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the parent Order.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// FK to the Product. Nullable — the product may be deleted after purchase,
    /// but the snapshot data (ProductName, UnitPrice) remains intact.
    /// </summary>
    public Guid? ProductId { get; private set; }

    /// <summary>
    /// Snapshot of the product name at the time of purchase.
    /// Never updated, even if the product is later renamed.
    /// </summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>
    /// Snapshot of the unit price at the time of purchase.
    /// Private setter ensures EF Core change-tracker cannot overwrite this
    /// via relationship fixup when the related Product entity is modified.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Number of units purchased. Must be greater than zero.</summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Stored total (UnitPrice * Quantity). Computed once at creation.
    /// Stored in the DB to avoid floating-point re-calculation on reads.
    /// </summary>
    public decimal Total { get; private set; }

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private OrderItem() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new OrderItem with immutable snapshot values.
    /// Only called from <see cref="Order.Create"/>.
    /// </summary>
    public static OrderItem Create(
        Guid orderId,
        Guid? productId,
        string productName,
        decimal unitPrice,
        int quantity)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId must not be empty.", nameof(orderId));
        ArgumentException.ThrowIfNullOrWhiteSpace(productName, nameof(productName));
        if (unitPrice < 0)
            throw new ArgumentException("UnitPrice must be non-negative.", nameof(unitPrice));
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName.Trim(),
            UnitPrice = unitPrice,
            Quantity = quantity,
            Total = unitPrice * quantity,  // snapshot computed once
        };
    }
}