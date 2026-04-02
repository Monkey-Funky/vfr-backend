
namespace Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="NotificationPreference"/>.
///
/// Applies to table: notification_preferences (snake_case via global UseSnakeCaseNamingConvention).
/// One record per retailer — unique FK index enforces the 1:1 relationship at DB level.
/// </summary>
public sealed class NotificationPreferenceConfiguration
    : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        // ── Table ─────────────────────────────────────────────────────────────
        builder.ToTable("notification_preferences");

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(n => n.Id);

        // ── FK — retailer_id ──────────────────────────────────────────────────
        builder.Property(n => n.RetailerId)
               .IsRequired();

        // ── Unique FK index (enforces 1:1 at DB level) ────────────────────────
        builder.HasIndex(n => n.RetailerId)
               .IsUnique()
               .HasDatabaseName("ux_notification_preferences_retailer_id");

        // ── Boolean alert columns (all default true = opted-in on creation) ───
        // Correct property names from NotificationPreference entity:
        builder.Property(n => n.LowStockAlerts).HasDefaultValue(true);
        builder.Property(n => n.OrderStatusAlerts).HasDefaultValue(true);
        builder.Property(n => n.SubscriptionAlerts).HasDefaultValue(true);

        // Correct channel names: EmailNotifications / InAppNotifications
        // (NOT EmailEnabled / PushEnabled — those names don't exist on the entity)
        builder.Property(n => n.EmailNotifications).HasDefaultValue(true);
        builder.Property(n => n.InAppNotifications).HasDefaultValue(true);
    }
}