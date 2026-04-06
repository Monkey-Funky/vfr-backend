using Application.Interfaces.Persistence;
namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Concrete implementation of IInventoryRepository.
/// Lives in Infrastructure — has access to the EF Core relational provider,
/// which provides AsAsyncEnumerable(), AsNoTracking(), and AsSplitQuery().
///
/// IMPORTANT:
///   AsAsyncEnumerable() is only available from the EF Core relational package
///   (Npgsql.EntityFrameworkCore.PostgreSQL). Never call it from the Application layer.
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
        // Base query — global query filter already excludes soft-deleted records
        var query = _context.InventoryRecords
            .AsNoTracking()
            .Where(r => r.RetailerId == retailerId);

        // Optional: partial case-insensitive product name filter
        if (!string.IsNullOrWhiteSpace(productNameFilter))
        {
            string normalized = productNameFilter.Trim().ToLower();
            query = query.Where(r =>
                r.ProductName.ToLower().Contains(normalized));
        }

        // Count before pagination
        int totalCount = await query.CountAsync(cancellationToken);

        // Sorting
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
        // NO AsNoTracking — the entity must be tracked so EF Core can:
        //   a) generate the correct UPDATE statement with RowVersion in the WHERE clause
        //   b) detect DbUpdateConcurrencyException when RowVersion has changed
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
        // AsAsyncEnumerable() available here because Infrastructure references
        // the Npgsql EF Core relational provider (not just the base EF Core package).
        return _context.InventoryRecords
            .AsNoTracking()
            .Where(r => r.RetailerId == retailerId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .AsAsyncEnumerable();
    }
}