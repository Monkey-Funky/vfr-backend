namespace Application.Interfaces;

/// <summary>
/// Unit of Work abstraction — wraps the EF Core DbContext for all command handlers.
///
/// RULE: Only command handlers inject IUnitOfWork.
///       Query handlers inject IApplicationDbContext directly.
///
/// CHANGE (P-017 BUG-001): Added GetTrackedByIdAsync<T> to support optimistic
///   concurrency inside ExecuteInTransactionAsync. This is a purely ADDITIVE change
///   and does NOT affect any existing Auth or other handlers — they continue to use
///   Repository<T>() and SaveChangesAsync as before.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>
    /// Returns (or creates) the generic repository for the given entity type.
    /// Repositories are created once per UnitOfWork lifetime and cached.
    /// </summary>
    IRepository<T> Repository<T>() where T : BaseEntity;

    /// <summary>
    /// Loads the entity by ID WITH EF Core change tracking enabled.
    ///
    /// PURPOSE: Required for optimistic concurrency inside ExecuteInTransactionAsync.
    ///   When the entity is loaded WITH tracking and the Subscription table has the
    ///   xmin shadow property configured as IsConcurrencyToken(), EF Core will:
    ///   1. Read the current xmin value from PostgreSQL on SELECT.
    ///   2. Include "WHERE xmin = @original" in the UPDATE statement.
    ///   3. Throw DbUpdateConcurrencyException if another transaction modified the row.
    ///
    /// IMPORTANT: Do NOT mix tracked and AsNoTracking queries for the same entity
    ///   within the same UnitOfWork scope — this causes EF Core ambiguity errors.
    ///   Use ONLY inside command handlers where change tracking is needed.
    /// </summary>
    Task<T?> GetTrackedByIdAsync<T>(Guid id, CancellationToken ct = default)
        where T : BaseEntity;

    /// <summary>
    /// Saves all pending changes in the current DbContext to the database.
    /// Does NOT open a transaction — use ExecuteInTransactionAsync for multi-step atomic writes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as SaveChangesAsync but returns true if at least one row was affected.
    /// </summary>
    Task<bool> SaveChangesReturnBoolAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the given operation inside a real PostgreSQL transaction,
    /// wrapped in EF Core's execution strategy for Npgsql retry compatibility.
    ///
    /// HOW TO USE:
    ///   await _unitOfWork.ExecuteInTransactionAsync(async ct =>
    ///   {
    ///       await _unitOfWork.Repository{Foo}().AddAsync(foo, ct);
    ///       await _unitOfWork.SaveChangesAsync(ct);   // ← INSIDE the lambda
    ///   }, cancellationToken);
    ///
    /// IMPORTANT: Call SaveChangesAsync INSIDE the operation lambda, not after.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}