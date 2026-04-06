using Domain.Enums.Product;
using Domain.Exceptions;

namespace Domain.Entities.Retailer;

/// <summary>
/// Represents the inventory state for a single Product belonging to a Retailer.
///
/// DESIGN RULES:
///   • One InventoryRecord per (retailer_id, product_id) pair — enforced by a
///     partial UNIQUE INDEX in the database.
///   • current_stock can never go below 0 — enforced by DB CHECK constraint AND
///     by every AdjustStock overload on this entity.
///   • RowVersion is the optimistic concurrency token (integer) — EF Core includes
///     it in every UPDATE WHERE clause via IsConcurrencyToken() mapping.
///   • Two AdjustStock overloads exist to serve two distinct callers:
///       – AdjustStock(delta, type, adjustedById)   ← called by InventoryDecrementHandler (order events)
///       – AdjustStock(newQuantity, type, reason, adjustedById) ← called by AdjustStockCommandHandler (manual API)
/// </summary>
public sealed class InventoryRecord : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the owning retailer. Never null after construction.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>FK to the parent Product. Never null after construction.</summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Denormalized snapshot of the product name at record creation time.
    /// Updated when the product name changes.
    /// </summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Current available stock. Always >= 0.</summary>
    public int CurrentStock { get; private set; }

    /// <summary>Lifetime units sold (incremented by order fulfillment).</summary>
    public int SoldQuantity { get; private set; }

    /// <summary>
    /// Stock level at or below which the status transitions to LowStock.
    /// Default 10 (matches DB default).
    /// </summary>
    public int LowStockThreshold { get; private set; } = 10;

    /// <summary>
    /// Optimistic concurrency token. Incremented on every EF Core save.
    /// Mapped via Property(e => e.RowVersion).IsConcurrencyToken() in EF config.
    /// </summary>
    public int RowVersion { get; private set; }

    /// <summary>Derived inventory status. Use <see cref="InventoryStatus"/> constants.</summary>
    public string Status { get; private set; } = InventoryStatus.InStock;

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private InventoryRecord() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates a new InventoryRecord for a product.
    /// Called atomically alongside Product.Create() in CreateProductCommandHandler.
    /// </summary>
    public static InventoryRecord Create(
        Guid retailerId,
        Guid productId,
        string productName,
        int initialQuantity = 0,
        int lowStockThreshold = 10)
    {
        if (retailerId == Guid.Empty)
            throw new ArgumentException("RetailerId must not be empty.", nameof(retailerId));

        if (productId == Guid.Empty)
            throw new ArgumentException("ProductId must not be empty.", nameof(productId));

        ArgumentException.ThrowIfNullOrWhiteSpace(productName, nameof(productName));

        if (initialQuantity < 0)
            throw new BusinessRuleException(
                "INVALID_QUANTITY",
                "Initial quantity cannot be negative.");

        if (lowStockThreshold < 0)
            throw new BusinessRuleException(
                "INVALID_THRESHOLD",
                "Low stock threshold cannot be negative.");

        return new InventoryRecord
        {
            RetailerId = retailerId,
            ProductId = productId,
            ProductName = productName.Trim(),
            CurrentStock = initialQuantity,
            SoldQuantity = 0,
            LowStockThreshold = lowStockThreshold,
            RowVersion = 0,
            Status = InventoryStatus.Derive(initialQuantity, lowStockThreshold)
        };
    }

    // =========================================================================
    // Domain Methods
    // =========================================================================

    /// <summary>
    /// Updates the denormalized product name snapshot when a product is renamed.
    /// </summary>
    public void UpdateProductName(string newProductName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newProductName, nameof(newProductName));
        ProductName = newProductName.Trim();
    }

    /// <summary>
    /// Adjusts the current stock by the given delta (positive = increase, negative = decrease).
    /// Called by <see cref="InventoryDecrementHandler"/> during order fulfilment.
    /// Throws when the resulting stock would be negative.
    /// </summary>
    /// <param name="delta">Units to add (positive) or remove (negative).</param>
    /// <param name="adjustmentType">One of the <see cref="AdjustmentType"/> constants.</param>
    /// <param name="adjustedById">Retailer account that triggered this change.</param>
    public void AdjustStock(int delta, string adjustmentType, Guid adjustedById)
    {
        int newStock = CurrentStock + delta;

        if (newStock < 0)
            throw new BusinessRuleException(
                "INSUFFICIENT_STOCK",
                $"Cannot reduce stock below zero. " +
                $"Current: {CurrentStock}, Requested delta: {delta}.");

        CurrentStock = newStock;

        if (adjustmentType == AdjustmentType.OrderSale && delta < 0)
            SoldQuantity += Math.Abs(delta);

        Status = InventoryStatus.Derive(CurrentStock, LowStockThreshold);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets stock to an absolute <paramref name="newQuantity"/> value.
    /// Called by <see cref="AdjustStockCommandHandler"/> for manual inventory adjustments via the API.
    ///
    /// DOMAIN INVARIANT:
    ///   <paramref name="newQuantity"/> must be >= 0. Negative values violate the stock floor
    ///   and throw <see cref="BusinessRuleException"/> with code "STOCK_FLOOR_VIOLATION".
    ///
    /// POST-CONDITION:
    ///   After adjustment, if <see cref="CurrentStock"/> &lt;= <see cref="LowStockThreshold"/>,
    ///   the caller (handler) MUST raise a <see cref="LowStockWarningEvent"/>.
    ///   The domain method does NOT raise the event itself — event dispatch is the handler's
    ///   responsibility so that it participates in the correct transaction scope.
    /// </summary>
    /// <param name="newQuantity">Target stock level. Must be >= 0.</param>
    /// <param name="type">Category of adjustment — must be a valid AdjustmentType constant.</param>
    /// <param name="reason">
    ///   Human-readable reason. Required when <paramref name="type"/> is
    ///   ManualIncrease or ManualDecrease. Optional for OrderSale and ReturnRestock.
    /// </param>
    /// <param name="adjustedById">ID of the retailer account performing the adjustment.</param>
    /// <returns>The stock quantity before this adjustment (used to build the audit record).</returns>
    public int AdjustStock(int newQuantity, string type, string? reason, Guid adjustedById)
    {
        if (newQuantity < 0)
            throw new BusinessRuleException(
                "STOCK_FLOOR_VIOLATION",
                $"New stock quantity cannot be negative. Provided value: {newQuantity}.");

        if (adjustedById == Guid.Empty)
            throw new ArgumentException("AdjustedById must not be empty.", nameof(adjustedById));

        int oldQuantity = CurrentStock;

        // For OrderSale decrements track sold units
        if (type == AdjustmentType.OrderSale && newQuantity < CurrentStock)
            SoldQuantity += CurrentStock - newQuantity;

        CurrentStock = newQuantity;
        Status = InventoryStatus.Derive(CurrentStock, LowStockThreshold);
        UpdatedAt = DateTime.UtcNow;

        return oldQuantity;
    }

    /// <summary>
    /// Decrements stock by the ordered quantity. Used exclusively by InventoryDecrementHandler.
    /// </summary>
    public void DecrementStock(int orderedQuantity, int newStockLevel)
    {
        if (orderedQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(orderedQuantity),
                "Ordered quantity must be greater than zero.");

        SoldQuantity += orderedQuantity;
        CurrentStock = newStockLevel;
        Status = InventoryStatus.Derive(CurrentStock, LowStockThreshold);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Soft-deletes this inventory record.
    /// Does NOT delete or alter the parent Product.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
    }
}