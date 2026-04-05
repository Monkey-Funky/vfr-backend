using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities.Retailer;


/// <summary>
/// Represents the inventory state for a single Product belonging to a Retailer.
///
/// DESIGN RULES:
///   • One InventoryRecord per (retailer_id, product_id) pair — enforced by
///     a partial UNIQUE INDEX in the database.
///   • current_stock can never go below 0 — enforced by DB CHECK constraint
///     AND by the AdjustStock domain method.
///   • RowVersion is the optimistic concurrency token (integer, incremented on
///     every update via EF Core IsConcurrencyToken).
///   • Created atomically with its parent Product inside one transaction.
///   • Soft-deleted atomically when its parent Product is soft-deleted.
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
    /// Denormalized snapshot of the product's name at record creation time.
    /// Updated when the product name changes (via UpdateProductCommand).
    /// </summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>Current available stock. Always >= 0.</summary>
    public int CurrentStock { get; private set; }

    /// <summary>Lifetime units sold (incremented by the order fulfillment subsystem).</summary>
    public int SoldQuantity { get; private set; }

    /// <summary>
    /// Stock level below which the status transitions to LowStock.
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
    /// <param name="retailerId">Owning retailer. Must not be empty.</param>
    /// <param name="productId">Parent product. Must not be empty.</param>
    /// <param name="productName">Snapshot of the product name at creation time.</param>
    /// <param name="initialQuantity">Starting stock. Must be >= 0. Defaults to 0.</param>
    /// <param name="lowStockThreshold">Alert threshold. Defaults to 10.</param>
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

        var status = InventoryStatus.Derive(initialQuantity, lowStockThreshold);

        return new InventoryRecord
        {
            RetailerId = retailerId,
            ProductId = productId,
            ProductName = productName.Trim(),
            CurrentStock = initialQuantity,
            SoldQuantity = 0,
            LowStockThreshold = lowStockThreshold,
            RowVersion = 0,
            Status = status
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
}