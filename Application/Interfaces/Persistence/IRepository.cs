using Domain.Entities.Customer;

namespace Application.Interfaces.Persistence;

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

    /// <summary>
    /// Returns the first entity matching <paramref name="predicate"/> ordered by
    /// <paramref name="orderBy"/>, or <c>null</c> if no match is found.
    /// Always supply <paramref name="orderBy"/> when more than one row could match
    /// to produce deterministic results and silence the EF W-4 warning.
    /// </summary>
    // ── W-4 FIX (ordered overload) ────────────────────────────────────────────
    // Use this overload when the predicate can match more than one row (e.g. an OR
    // predicate across two unique columns). The explicit OrderBy makes the result
    // deterministic and suppresses the EF Core "FirstOrDefault without OrderBy" warning.
    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the single entity matching <paramref name="predicate"/>,
    /// or <c>null</c> if no match is found.
    /// Throws <see cref="InvalidOperationException"/> if more than one entity matches.
    /// </summary>
    // ── W-4 FIX (SingleOrDefault) ─────────────────────────────────────────────
    // Use for queries on primary keys or unique indexes (guaranteed 0 or 1 rows).
    // EF Core does not emit the "no OrderBy" warning for Single/SingleOrDefault,
    // and the semantics more accurately express the intent of a unique lookup.
    Task<T?> SingleOrDefaultAsync(
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