using Domain.Entities.Analytics;

namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class DashboardSnapshotConfiguration
    : IEntityTypeConfiguration<DashboardSnapshot>
{
    public void Configure(EntityTypeBuilder<DashboardSnapshot> builder)
    {
        builder.ToTable("dashboard_snapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.RetailerId).IsRequired();

        builder.Property(s => s.SnapshotDate)
               .IsRequired()
               .HasColumnType("date");

        builder.Property(s => s.TotalRevenue)
               .HasColumnType("numeric(18,2)")
               .HasDefaultValue(0m)
               .IsRequired();

        builder.Property(s => s.TotalProfit)
               .HasColumnType("numeric(18,2)")
               .HasDefaultValue(0m)
               .IsRequired();

        builder.Property(s => s.TotalOrders).HasDefaultValue(0).IsRequired();
        builder.Property(s => s.ActiveProducts).HasDefaultValue(0).IsRequired();
        builder.Property(s => s.LowStockCount).HasDefaultValue(0).IsRequired();

        builder.Property(s => s.ConversionRate)
               .HasColumnType("numeric(5,4)")
               .HasDefaultValue(0m)
               .IsRequired();

        builder.Property(s => s.TryOnEngagement).HasDefaultValue(0).IsRequired();

        builder.Property(s => s.ComputedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        // FK to retailer_accounts
        builder.HasIndex(s => s.RetailerId)
               .HasDatabaseName("idx_dashboard_snapshots_retailer_id");

        // Composite index for date-range queries scoped by retailer
        builder.HasIndex(s => new { s.RetailerId, s.SnapshotDate })
               .HasDatabaseName("idx_dashboard_snapshots_retailer_date");

        // Unique: one snapshot per retailer per date
        builder.HasIndex(s => new { s.RetailerId, s.SnapshotDate })
               .IsUnique()
               .HasDatabaseName("uidx_dashboard_snapshots_retailer_date");
    }
}