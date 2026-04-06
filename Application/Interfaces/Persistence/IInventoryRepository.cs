namespace Application.Interfaces.Persistence;

/// <summary>
/// Repository contract for InventoryRecord read operations that require EF Core
/// relational features (Include, AsAsyncEnumerable, pagination) not available
/// via the generic IRepository{T}.
///
/// Write operations (Add, Update, soft-delete) continue to use IUnitOfWork.Repository{T}.
/// </summary>
public interface IInventoryRepository
{
    /// <summary>
    /// Returns a paginated list of inventory records for the given retailer,
    /// optionally filtered by product name and sorted by sold quantity.
    /// </summary>
    Task<(IReadOnlyList<InventoryRecord> Items, int TotalCount)> GetPagedAsync(
        Guid retailerId,
        string? productNameFilter,
        bool sortBySoldQuantityDesc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the inventory record for a specific product owned by the retailer.
    /// Returns null if not found or if the record is soft-deleted.
    /// </summary>
    Task<InventoryRecord?> GetByProductIdAsync(
        Guid retailerId,
        Guid productId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a tracked (non-AsNoTracking) InventoryRecord by its primary key.
    /// Required by AdjustStockCommandHandler for optimistic concurrency via RowVersion.
    /// Returns null when not found or soft-deleted.
    /// </summary>
    Task<InventoryRecord?> GetTrackedByIdAsync(
        Guid retailerId,
        Guid inventoryRecordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all inventory records for the retailer as an IAsyncEnumerable.
    /// Used by ExportInventoryCsvQueryHandler. Never returns soft-deleted records.
    /// </summary>
    IAsyncEnumerable<InventoryRecord> StreamAsync(
        Guid retailerId,
        CancellationToken cancellationToken = default);
}