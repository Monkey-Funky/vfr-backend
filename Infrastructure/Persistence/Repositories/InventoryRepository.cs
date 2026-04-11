using Application.Interfaces.Persistence;
namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Concrete implementation of IInventoryRepository.
/// Uses AsAsyncEnumerable() from the EF Core relational provider (Npgsql).
/// </summary>
public sealed class InventoryRepository : IInventoryRepository
{
    private readonly IApplicationDbContext _context;

    public InventoryRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InventoryRecord> Items, int TotalCount)> GetPagedAsync(
        Guid retailerId,
        string? productNameFilter,
        bool sortBySoldQuantityDesc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryRecords
            .AsNoTracking()
            .Where(r => r.RetailerId == retailerId);

        if (!string.IsNullOrWhiteSpace(productNameFilter))
        {
            string normalized = productNameFilter.Trim().ToLower();
            query = query.Where(r => r.ProductName.ToLower().Contains(normalized));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        query = sortBySoldQuantityDesc
            ? query.OrderByDescending(r => r.SoldQuantity)
            : query.OrderByDescending(r => r.CreatedAt);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<InventoryRecord?> GetByProductIdAsync(
        Guid retailerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await _context.InventoryRecords
            .AsNoTracking()
            // AdjustedAt returns CreatedAt — valid after StockAdjustment fix
            .Include(r => r.StockAdjustments.OrderByDescending(a => a.AdjustedAt))
            .FirstOrDefaultAsync(
                r => r.RetailerId == retailerId
                  && r.ProductId == productId
                  && !r.IsDeleted,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<InventoryRecord?> GetTrackedByIdAsync(
        Guid retailerId,
        Guid inventoryRecordId,
        CancellationToken cancellationToken = default)
    {
        // NO AsNoTracking — entity must be tracked for optimistic concurrency (RowVersion)
        return await _context.InventoryRecords
            .FirstOrDefaultAsync(
                r => r.Id == inventoryRecordId
                  && r.RetailerId == retailerId
                  && !r.IsDeleted,
                cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<InventoryRecord> StreamAsync(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        return _context.InventoryRecords
            .AsNoTracking()
            .Where(r => r.RetailerId == retailerId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .AsAsyncEnumerable();
    }
}