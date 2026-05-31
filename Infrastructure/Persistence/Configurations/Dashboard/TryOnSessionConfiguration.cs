using Domain.Entities.Analytics;

namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class TryOnSessionConfiguration
    : IEntityTypeConfiguration<TryOnSession>
{
    public void Configure(EntityTypeBuilder<TryOnSession> builder)
    {
        builder.ToTable("try_on_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.RetailerId).IsRequired();
        builder.Property(s => s.ProductId);
        builder.Property(s => s.CustomerId);

        builder.Property(s => s.SessionDurationSeconds)
               .HasDefaultValue(0)
               .IsRequired();

        builder.Property(s => s.ResultedInPurchase)
               .HasDefaultValue(false)
               .IsRequired();

        builder.Property(s => s.CreatedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        builder.HasIndex(s => s.RetailerId)
               .HasDatabaseName("idx_try_on_sessions_retailer_id");

        builder.HasIndex(s => s.ProductId)
               .HasDatabaseName("idx_try_on_sessions_product_id");

        // Composite index on (retailer_id, created_at) — covers the date-range
        // filter pattern used by all dashboard KPI and chart queries:
        //   WHERE retailer_id = @id AND created_at >= @from AND created_at < @to
        // Without this, Postgres falls back to a seq-scan or single-column index
        // scan + filter, which is an O(N) full table scan on large datasets.
        // With this index the planner can satisfy the predicate entirely from the
        // B-tree, reducing those queries from O(N) to O(log N + result set size).
        builder.HasIndex(s => new { s.RetailerId, s.CreatedAt })
               .HasDatabaseName("idx_try_on_sessions_retailer_createdat");
    }
}
