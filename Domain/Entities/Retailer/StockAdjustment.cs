
namespace Domain.Entities.Retailer;

/// <summary>
/// Immutable audit record of every stock quantity change on an InventoryRecord.
///
/// WHY IT EXTENDS BaseEntity:
///   IUnitOfWork.Repository{T} has constraint where T : BaseEntity.
///   StockAdjustment must extend BaseEntity to be used in event handlers via
///   _unitOfWork.Repository{StockAdjustment}().AddAsync(...).
///
/// COLUMN MAPPING NOTES:
///   • BaseEntity.CreatedAt maps to the "adjusted_at" DB column (see EF config).
///   • AdjustedAt is a computed property that returns CreatedAt — used by
///     InventoryMappings and InventoryRepository for ordering and projection.
///   • UpdatedAt, CreatedBy, UpdatedBy are ignored in EF config (not in DB schema).
///   • IsDeleted is ignored in EF config — adjustment records are never soft-deleted;
///     they are immutable audit entries.
///
/// IMMUTABILITY: Once created, a StockAdjustment is never modified.
/// All properties are private-set and only populated via the Create factory method.
/// </summary>
public sealed class StockAdjustment : BaseEntity
{
    // =========================================================================
    // Properties
    // =========================================================================

    /// <summary>FK to the InventoryRecord this adjustment belongs to.</summary>
    public Guid InventoryRecordId { get; private set; }

    /// <summary>
    /// Type of adjustment. Use AdjustmentType constants:
    /// 'ManualIncrease' | 'ManualDecrease' | 'OrderSale' | 'ReturnRestock'
    /// </summary>
    public string AdjustmentType { get; private set; } = string.Empty;

    /// <summary>Stock quantity BEFORE this adjustment was applied.</summary>
    public int OldQuantity { get; private set; }

    /// <summary>Stock quantity AFTER this adjustment was applied.</summary>
    public int NewQuantity { get; private set; }

    /// <summary>Optional human-readable reason (e.g., "Returned goods — invoice #1234").</summary>
    public string? Reason { get; private set; }

    /// <summary>ID of the retailer account that triggered this adjustment.</summary>
    public Guid AdjustedById { get; private set; }

    /// <summary>
    /// When this adjustment was recorded. Computed from BaseEntity.CreatedAt.
    /// The EF Core configuration maps BaseEntity.CreatedAt to the "adjusted_at"
    /// column — AdjustedAt is a domain-level alias used by mappings and repositories.
    /// </summary>
    public DateTime AdjustedAt => CreatedAt;

    // =========================================================================
    // EF Core Constructor (private — do not call directly)
    // =========================================================================

    private StockAdjustment() { }

    // =========================================================================
    // Factory Method
    // =========================================================================

    /// <summary>
    /// Creates an immutable StockAdjustment audit record.
    /// Called from AdjustStockCommandHandler and InventoryDecrementHandler
    /// after successfully updating the parent InventoryRecord.
    /// </summary>
    public static StockAdjustment Create(
        Guid inventoryRecordId,
        string adjustmentType,
        int oldQuantity,
        int newQuantity,
        Guid adjustedById,
        string? reason = null)
    {
        if (inventoryRecordId == Guid.Empty)
            throw new ArgumentException(
                "InventoryRecordId must not be empty.", nameof(inventoryRecordId));

        ArgumentException.ThrowIfNullOrWhiteSpace(adjustmentType, nameof(adjustmentType));

        return new StockAdjustment
        {
            Id = Guid.NewGuid(),
            InventoryRecordId = inventoryRecordId,
            AdjustmentType = adjustmentType,
            OldQuantity = oldQuantity,
            NewQuantity = newQuantity,
            AdjustedById = adjustedById,
            Reason = reason,
            // BaseEntity.CreatedAt is set by ApplicationDbContext.SaveChangesAsync.
            // It maps to the "adjusted_at" column in DB via StockAdjustmentConfiguration.
        };
    }
}

/// <summary>
/// AdjustmentType string constants. Matches DB CHECK constraint values.
/// </summary>
public static class AdjustmentType
{
    public const string ManualIncrease = "ManualIncrease";
    public const string ManualDecrease = "ManualDecrease";
    public const string OrderSale = "OrderSale";
    public const string ReturnRestock = "ReturnRestock";
}