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
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(n => n.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(n => n.Title)
            .HasColumnName("title")
            .HasColumnType("varchar(200)")
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
            .HasColumnName("read_at")
            .IsRequired(false);

        builder.Property(n => n.ResourceId)
            .HasColumnName("resource_id")
            .IsRequired(false);

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.HasCheckConstraint(
            "chk_notifications_type",
            "type IN ('LowStock','OrderStatusChanged','SubscriptionExpiring','PaymentFailed','AccountDeletion')");

        // Indexes
        builder.HasIndex(n => new { n.RetailerId, n.CreatedAt })
            .HasDatabaseName("idx_notifications_retailer_createdat")
            .IsDescending(false, true);

        builder.HasIndex(n => n.RetailerId)
            .HasDatabaseName("idx_notifications_retailer_id");

        builder.HasIndex(n => new { n.RetailerId, n.IsRead })
            .HasDatabaseName("idx_notifications_retailer_unread")
            .HasFilter("is_read = false");

        // Ignore audit fields not in DB schema for this entity
        builder.Ignore(n => n.UpdatedAt);
        builder.Ignore(n => n.UpdatedBy);
        builder.Ignore(n => n.CreatedBy);
        builder.Ignore(n => n.IsDeleted);
    }
}