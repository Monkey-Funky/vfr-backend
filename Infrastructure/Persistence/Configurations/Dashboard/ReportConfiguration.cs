using Domain.Entities.Analytics;
using Domain.Enums.Analytics;


namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.RetailerId).IsRequired();

        builder.Property(r => r.Status)
               .HasMaxLength(20)
               .HasConversion<string>()
               .HasDefaultValue(ReportStatus.Pending)
               .IsRequired();

        builder.HasCheckConstraint(
            "ck_reports_status",
            "status IN ('Pending','Processing','Ready','Failed')");

        builder.Property(r => r.RangeFrom).HasColumnType("date").IsRequired();
        builder.Property(r => r.RangeTo).HasColumnType("date").IsRequired();
        builder.Property(r => r.ReportUrl).HasColumnType("text");
        builder.Property(r => r.FailureReason).HasColumnType("text");

        builder.Property(r => r.CreatedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        builder.Property(r => r.CompletedAt)
               .HasColumnType("timestamptz");

        builder.HasIndex(r => r.RetailerId)
               .HasDatabaseName("idx_reports_retailer_id");

        builder.HasIndex(r => new { r.RetailerId, r.Status })
               .HasDatabaseName("idx_reports_retailer_status");
    }
}