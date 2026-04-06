using Domain.Entities.Orders;

namespace Application.Interfaces.Persistence;

/// <summary>
/// Custom repository for Order queries that are too complex for the generic IRepository{Order}.
///
/// USAGE RULES:
///   • Query handlers inject IOrderRepository for read operations.
///   • Command handlers use IUnitOfWork.Repository{Order}() for tracked writes.
///   • All methods here are AsNoTracking — this is a read-only repository.
/// </summary>
public interface IOrderRepository : IRepository<Order>
{
    /// <summary>
    /// Returns a paginated, filtered list of orders for the specified retailer.
    /// Applies status filter and customer name / order ID search if provided.
    /// </summary>
    Task<(IReadOnlyList<Order> Items, int TotalCount)> GetPagedOrdersAsync(
        Guid retailerId,
        string? statusFilter,
        string? searchTerm,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a single order with its items, scoped to the retailer (IDOR protection).
    /// Returns null if not found or does not belong to retailerId.
    /// </summary>
    Task<Order?> GetByIdWithItemsAsync(
        Guid orderId,
        Guid retailerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams all orders for the retailer as an async enumerable.
    /// Used by ExportOrdersCsvQueryHandler for memory-efficient CSV streaming.
    /// The caller MUST pass the CancellationToken to .WithCancellation() to ensure
    /// the underlying DataReader is disposed on client disconnect.
    /// </summary>
    IAsyncEnumerable<Order> StreamOrdersAsync(
        Guid retailerId,
        CancellationToken cancellationToken = default);
}
