namespace Application.Interfaces;

public interface IRepository<T> where T : BaseEntity
{
    // ── Queries ─────────────────────────────────────────────────────────────
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    // ── Pagination ───────────────────────────────────────────────────────────
    Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    // ── Commands ─────────────────────────────────────────────────────────────
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    Task DeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default);

    // ── Soft Delete ──────────────────────────────────────────────────────────
    Task SoftDeleteAsync(T entity, CancellationToken cancellationToken = default);

    Task SoftDeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default);

    // ── Aggregates ───────────────────────────────────────────────────────────
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);
}