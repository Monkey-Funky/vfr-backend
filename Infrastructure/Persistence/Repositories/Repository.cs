using Application.Interfaces.Persistence;

namespace Infrastructure.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(ApplicationDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<T>> GetAllAsync(
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<T>> FindAsync(
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync(cancellationToken);

    public async Task<T?> FirstOrDefaultAsync(
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, cancellationToken);

    // ── W-4 FIX — ordered overload ────────────────────────────────────────────
    /// <inheritdoc />
    /// <remarks>
    /// Use this overload when the predicate can return more than one row (e.g. an OR
    /// predicate across multiple unique columns). The caller-supplied <paramref name="orderBy"/>
    /// makes the result deterministic and suppresses the EF Core W-4 runtime warning.
    /// </remarks>
    public Task<T?> FirstOrDefaultAsync(
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IOrderedQueryable<T>> orderBy,
        CancellationToken cancellationToken = default)
        => orderBy(DbSet.AsNoTracking().Where(predicate))
            .FirstOrDefaultAsync(cancellationToken);

    // ── W-4 FIX — SingleOrDefaultAsync ───────────────────────────────────────
    /// <inheritdoc />
    /// <remarks>
    /// Use for queries on primary keys or unique indexes where exactly 0 or 1 rows
    /// are expected. EF Core does not emit the "no OrderBy" warning for
    /// Single/SingleOrDefault. Throws <see cref="InvalidOperationException"/> if
    /// more than one row matches (which would indicate a data integrity violation).
    /// </remarks>
    public Task<T?> SingleOrDefaultAsync(
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        => DbSet
            .AsNoTracking()
            .Where(predicate)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (predicate is not null)
            query = query.Where(predicate);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task AddRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
        => await DbSet.AddRangeAsync(entities, cancellationToken);

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public Task DeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        DbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public Task SoftDeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        // DbContext.SetAuditFields() intercepts EntityState.Deleted and converts
        // it to a soft delete automatically — no need to set IsDeleted manually here
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public Task SoftDeleteRangeAsync(
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        DbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public async Task<int> CountAsync(
        System.Linq.Expressions.Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
        => predicate is null
            ? await DbSet.CountAsync(cancellationToken)
            : await DbSet.CountAsync(predicate, cancellationToken);

    public async Task<bool> AnyAsync(
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
        => await DbSet.AnyAsync(predicate, cancellationToken);
}