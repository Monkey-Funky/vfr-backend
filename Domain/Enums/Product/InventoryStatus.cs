namespace Domain.Enums.Product;


/// <summary>
/// Lifecycle status values for an InventoryRecord.
/// Stored as varchar(20) in the database.
/// </summary>
public static class InventoryStatus
{
    public const string InStock = "InStock";
    public const string LowStock = "LowStock";
    public const string OutOfStock = "OutOfStock";

    public static readonly IReadOnlyList<string> All = [InStock, LowStock, OutOfStock];

    /// <summary>
    /// Derives the correct status from the current stock level and threshold.
    /// </summary>
    public static string Derive(int currentStock, int lowStockThreshold)
    {
        if (currentStock <= 0)
            return OutOfStock;
        if (currentStock <= lowStockThreshold)
            return LowStock;
        return InStock;
    }
}
