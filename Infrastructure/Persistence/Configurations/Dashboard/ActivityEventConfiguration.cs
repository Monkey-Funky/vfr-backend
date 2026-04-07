
namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class ActivityEventConfiguration
    : IEntityTypeConfiguration<Domain.Entities.Analytics.ActivityEvent>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Analytics.ActivityEvent> builder)
    {
        builder.ToTable("activity_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.RetailerId).IsRequired();

        builder.Property(e => e.EventType)
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(e => e.ResourceId);

        builder.Property(e => e.EventData)
               .HasColumnType("jsonb")
               .HasDefaultValue("{}")
               .IsRequired();

        builder.Property(e => e.CreatedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        // FK index
        builder.HasIndex(e => e.RetailerId)
               .HasDatabaseName("idx_activity_events_retailer_id");

        // The critical composite index for GetRealTimeActivityQuery.
        // Enables ORDER BY created_at DESC without a full table scan.
        builder.HasIndex(e => new { e.RetailerId, e.CreatedAt })
               .IsDescending(false, true)
               .HasDatabaseName("idx_activity_events_retailer_createdat");
    }
}