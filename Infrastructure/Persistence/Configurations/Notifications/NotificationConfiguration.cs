using Domain.Entities.Notifications;

namespace Infrastructure.Persistence.Configurations.Notifications;

/// <summary>
/// EF Core configuration for the Notification entity.
///
/// TABLE: notifications
///
/// KEY DECISIONS:
///   • No global soft-delete query filter — notifications are not soft-deleted.
///     The DB schema has no is_deleted column (confirmed in B.16).
///   • BaseEntity.UpdatedAt, CreatedBy, UpdatedBy are IGNORED — not present in the
///     notifications DB schema (B.16 only has created_at).
///   • BaseEntity.IsDeleted is IGNORED — same reason.
///   • read_at is nullable (null when IsRead == false).
///   • resource_id is nullable (points to the affected resource, or null).
///   • DB CHECK constraint on type column enforces the NotificationType constants.
///   • FK index on retailer_id (PostgreSQL does not auto-index FK columns).
/// </summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(n => n.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.Title)
            .HasColumnName("title")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasColumnName("body")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(n => n.IsRead)
            .HasColumnName("is_read")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(n => n.ReadAt)
            .HasColumnName("read_at");

        builder.Property(n => n.ResourceId)
            .HasColumnName("resource_id");

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // BaseEntity fields not persisted for notifications
        builder.Ignore(n => n.UpdatedAt);
        builder.Ignore(n => n.CreatedBy);
        builder.Ignore(n => n.UpdatedBy);
        builder.Ignore(n => n.IsDeleted);

        // FK to retailer
        builder.HasOne<Domain.Entities.Retailer.RetailerAccount>()
            .WithMany()
            .HasForeignKey(n => n.RetailerId)
            .OnDelete(DeleteBehavior.Cascade);

        // CHECK constraint: type must be a known value
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_notifications_type",
            "type IN ('LowStock','NewOrder','OrderStatusChanged','SubscriptionExpiring','PaymentFailed','SystemAlert')"));

        // Composite index: (retailer_id, created_at DESC) — primary read pattern
        builder.HasIndex(n => new { n.RetailerId, n.CreatedAt })
            .HasDatabaseName("idx_notifications_retailer_created_at")
            .IsDescending(false, true);

        // Index for unread count queries
        builder.HasIndex(n => new { n.RetailerId, n.IsRead })
            .HasDatabaseName("idx_notifications_retailer_is_read");
    }
}