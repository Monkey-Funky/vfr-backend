using Domain.Entities.Analytics;

namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class FitAccuracyConfiguration
    : IEntityTypeConfiguration<FitAccuracy>
{
    public void Configure(EntityTypeBuilder<FitAccuracy> builder)
    {
        builder.ToTable("fit_accuracies");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.RetailerId).IsRequired();
        builder.Property(f => f.ProductId);

        builder.Property(f => f.PredictedSize)
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(f => f.ActualSize)
               .HasMaxLength(20)
               .IsRequired();

        builder.Property(f => f.WasAccurate)
               .HasDefaultValue(false)
               .IsRequired();

        builder.Property(f => f.SessionId);

        builder.Property(f => f.RecordedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        builder.HasIndex(f => f.RetailerId)
               .HasDatabaseName("idx_fit_accuracies_retailer_id");

        builder.HasIndex(f => new { f.RetailerId, f.RecordedAt })
               .HasDatabaseName("idx_fit_accuracies_retailer_recordedat");
    }
}