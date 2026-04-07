using Domain.Entities.Analytics;

namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class VfrEngagementMetricConfiguration
    : IEntityTypeConfiguration<VfrEngagementMetric>
{
    public void Configure(EntityTypeBuilder<VfrEngagementMetric> builder)
    {
        builder.ToTable("vfr_engagement_metrics");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.RetailerId).IsRequired();
        builder.Property(m => m.MetricDate).HasColumnType("date").IsRequired();
        builder.Property(m => m.TotalTryOns).HasDefaultValue(0).IsRequired();
        builder.Property(m => m.UniqueCustomers).HasDefaultValue(0).IsRequired();

        builder.Property(m => m.AvgSessionSeconds)
               .HasColumnType("numeric(10,2)")
               .HasDefaultValue(0m)
               .IsRequired();

        builder.Property(m => m.ConversionRate)
               .HasColumnType("numeric(5,4)")
               .HasDefaultValue(0m)
               .IsRequired();

        builder.Property(m => m.TopProductId);

        builder.Property(m => m.RecordedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        builder.HasIndex(m => m.RetailerId)
               .HasDatabaseName("idx_vfr_engagement_metrics_retailer_id");
    }
}