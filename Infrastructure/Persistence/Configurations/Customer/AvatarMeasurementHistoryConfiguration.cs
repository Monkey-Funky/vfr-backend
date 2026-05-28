using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

public sealed class AvatarMeasurementHistoryConfiguration : IEntityTypeConfiguration<AvatarMeasurementHistory>
{
    public void Configure(EntityTypeBuilder<AvatarMeasurementHistory> builder)
    {
        builder.ToTable("avatar_measurement_history", t =>
        {
            t.HasCheckConstraint("ck_avatar_measurement_history_source",
                "source IN ('Manual', 'BodyScan', 'AIEstimate')");
        });

        builder.HasKey(h => h.Id);

        builder.Property(h => h.MeasurementData)
            .HasColumnName("measurement_data")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(h => h.Source)
            .HasColumnName("source")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(h => h.RecordedAt)
            .HasColumnName("recorded_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Ignore auditing fields not present in the immutable history table.
        builder.Ignore(h => h.UpdatedAt);
        builder.Ignore(h => h.CreatedBy);
        builder.Ignore(h => h.UpdatedBy);
        builder.Ignore(h => h.IsDeleted);

        // Note: the HasOne/WithMany relationship is configured from the Avatar side
        // in AvatarConfiguration using HasMany → WithOne. Configuring it here too
        // (even with .WithMany(a => a.MeasurementHistories)) caused EF Core to register
        // two relationships — one from convention (FK = AvatarId) and one from the explicit
        // config — resulting in the shadow FK column "avatar_id1".

        builder.HasIndex(h => new { h.AvatarId, h.RecordedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_avatar_measurement_history_avatar_recorded");
    }
}