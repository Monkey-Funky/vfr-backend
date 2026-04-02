using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure.Persistence;

/// <summary>
/// Concrete implementation of IUnitOfWork.
///
/// CHANGE (P-017 BUG-001): Added GetTrackedByIdAsync{T} — loads an entity WITH
///   EF Core change tracking so that the xmin concurrency token is included in
///   subsequent UPDATE statements. This is a purely additive change.
///   Auth handlers are NOT affected — they only use Repository{T}() and SaveChangesAsync.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories = [];

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Repository factory ────────────────────────────────────────────────────

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        var type = typeof(T);

        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new Repository<T>(_context);
            _repositories[type] = repository;
        }

        return (IRepository<T>)repository;
    }

    // ── Tracked query for optimistic concurrency (BUG-001 FIX) ───────────────

    /// <inheritdoc />
    public async Task<T?> GetTrackedByIdAsync<T>(Guid id, CancellationToken ct = default)
        where T : BaseEntity
    {
        // NO AsNoTracking() here — change tracking is REQUIRED so that EF Core
        // includes the xmin concurrency token value in UPDATE WHERE clauses.
        // Any concurrent row modification will cause DbUpdateConcurrencyException.
        return await _context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public async Task<bool> SaveChangesReturnBoolAsync(
        CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken) > 0;

    // ── Transactional execution ───────────────────────────────────────────────

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            await using IDbContextTransaction transaction =
                await _context.Database.BeginTransactionAsync(ct);

            try
            {
                await operation(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch
                {
                    // Swallow rollback failure — original exception is more important
                }

                throw;
            }
        }, cancellationToken);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }
}