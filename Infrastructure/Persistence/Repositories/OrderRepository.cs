using Application.Interfaces.Persistence;
using Domain.Entities.Orders;


namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Concrete implementation of IOrderRepository.
///
/// ALL queries are AsNoTracking — this is a read repository.
/// Command handlers use IUnitOfWork.Repository{Order}() and
/// GetTrackedByIdAsync{Order}() for tracked writes.
/// </summary>
public sealed class OrderRepository : Repository<Order>, IOrderRepository
{
    private readonly ApplicationDbContext _context;

    public OrderRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedOrdersAsync(
        Guid retailerId,
        string? statusFilter,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.RetailerId == retailerId && !o.IsDeleted);

        // Status filter — exact match (validated upstream by FluentValidation)
        if (!string.IsNullOrWhiteSpace(statusFilter))
            query = query.Where(o => o.Status == statusFilter);

        // Search — by customer name (case-insensitive) or order ID prefix
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(o =>
                o.CustomerName.ToLower().Contains(term) ||
                o.Id.ToString().StartsWith(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<Order?> GetByIdWithItemsAsync(
        Guid orderId,
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        // ✅ Scopes to BOTH orderId AND retailerId — IDOR protection.
        // Returns null (→ NotFoundException → 404) if orderId belongs to another retailer.
        return await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(
                o => o.Id == orderId
                  && o.RetailerId == retailerId
                  && !o.IsDeleted,
                cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<Order> StreamOrdersAsync(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        // ✅ AsAsyncEnumerable() is available here because this class is in Infrastructure,
        //    which references Npgsql.EntityFrameworkCore.PostgreSQL (relational provider).
        //    The Application layer only has Microsoft.EntityFrameworkCore (no AsSplitQuery / streaming).
        return _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.RetailerId == retailerId && !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .AsAsyncEnumerable();
    }
}