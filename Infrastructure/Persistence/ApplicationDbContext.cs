using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>, IApplicationDbContext
{
    private readonly IDateTime _dateTime;
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDateTime dateTime,
        ICurrentUserService currentUserService)
        : base(options)
    {
        _dateTime = dateTime;
        _currentUserService = currentUserService;
    }

    // ── DbSet properties are added here as entities are created ──────────────
    // Example added in P-011:
    // public DbSet<RetailerAccount> RetailerAccounts => Set<RetailerAccount>();

    // ── EF Core Model Configuration ───────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // All IEntityTypeConfiguration<T> classes in this assembly are applied automatically.
        // Add configuration files to Infrastructure/Persistence/Configurations/ per prompt.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ApplySoftDeleteGlobalFilter(modelBuilder);
    }

    // ── SaveChanges Overrides ─────────────────────────────────────────────────

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return await base.SaveChangesAsync(cancellationToken);
    }

    // Sync override — guards against anyone calling the sync version accidentally
    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Sets CreatedAt/CreatedBy on Added entries and UpdatedAt/UpdatedBy on Modified entries.
    ///
    /// KEY DESIGN DECISION:
    /// We use entry.Property(...).CurrentValue instead of direct property assignment.
    /// This lets EF Core bypass the C# 'protected set' access modifier using its internal
    /// reflection mechanism — so BaseEntity.cs requires ZERO changes. The Domain layer
    /// stays pure. The audit concern belongs entirely to Infrastructure.
    /// </summary>
    private void SetAuditFields()
    {
        var now = _dateTime.UtcNow;
        var currentUser = _currentUserService.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // EF Core sets these through its internal reflection — bypasses 'protected set'
                    entry.Property(nameof(BaseEntity.CreatedAt)).CurrentValue = now;
                    entry.Property(nameof(BaseEntity.CreatedBy)).CurrentValue = currentUser;

                    // Ensure these are never accidentally set on a new entity
                    entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = null;
                    entry.Property(nameof(BaseEntity.UpdatedBy)).CurrentValue = null;

                    // Ensure soft-delete flag is always false on creation
                    entry.Property(nameof(BaseEntity.IsDeleted)).CurrentValue = false;
                    break;

                case EntityState.Modified:
                    entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(BaseEntity.UpdatedBy)).CurrentValue = currentUser;

                    // Prevent any external code from overwriting CreatedAt / CreatedBy
                    // on an update — these are immutable after creation
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    // Convert hard deletes into soft deletes automatically.
                    // Any call to DbSet.Remove() is intercepted here and turned into
                    // a soft delete — nothing is ever physically removed unless you
                    // explicitly call Database.ExecuteSqlRaw().
                    entry.State = EntityState.Modified;
                    entry.Property(nameof(BaseEntity.IsDeleted)).CurrentValue = true;
                    entry.Property(nameof(BaseEntity.UpdatedAt)).CurrentValue = now;
                    entry.Property(nameof(BaseEntity.UpdatedBy)).CurrentValue = currentUser;
                    break;
            }
        }
    }

    /// <summary>
    /// Applies a global query filter on every entity that inherits BaseEntity
    /// so that soft-deleted records are automatically excluded from all queries.
    /// You never need to add .Where(e => !e.IsDeleted) manually anywhere.
    ///
    /// To intentionally query deleted records use: .IgnoreQueryFilters()
    /// Example: _context.Products.IgnoreQueryFilters().Where(p => p.IsDeleted)
    /// </summary>
    private static void ApplySoftDeleteGlobalFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = System.Linq.Expressions.Expression
                .Parameter(entityType.ClrType, "e");

            var property = System.Linq.Expressions.Expression
                .Property(parameter, nameof(BaseEntity.IsDeleted));

            var falseConstant = System.Linq.Expressions.Expression
                .Constant(false);

            var filter = System.Linq.Expressions.Expression
                .Lambda(
                    System.Linq.Expressions.Expression.Equal(property, falseConstant),
                    parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}