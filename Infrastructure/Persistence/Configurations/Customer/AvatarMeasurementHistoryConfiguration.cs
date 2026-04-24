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

        // Ignore Auditing fields not present in immutable table
        builder.Ignore(h => h.UpdatedAt);
        builder.Ignore(h => h.CreatedBy);
        builder.Ignore(h => h.UpdatedBy);
        builder.Ignore(h => h.IsDeleted);

        // Configure relationship with Avatar
        builder.HasOne<Avatar>()
            .WithMany() 
            .HasForeignKey(h => h.AvatarId)
            .HasConstraintName("fk_avatar_measurement_history_avatars_avatar_id");

        builder.HasIndex(h => new { h.AvatarId, h.RecordedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_avatar_measurement_history_avatar_recorded");
    }
}
