namespace Infrastructure.Persistence.Configurations.Retailer;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="NotificationPreference"/> entity.
/// Table: notification_preferences
///
/// Constraints:
///   • One record per retailer: UNIQUE (retailer_id).
///   • All boolean columns default to TRUE in the database (matches CreateDefault factory).
/// </summary>
public sealed class NotificationPreferenceConfiguration
    : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(np => np.Id);

        builder.Property(np => np.Id)
            .HasColumnName("id")
            .IsRequired();

        // ── FK — one record per retailer ──────────────────────────────────────
        builder.Property(np => np.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.HasIndex(np => np.RetailerId)
            .IsUnique()
            .HasDatabaseName("uq_notification_preferences_retailer_id");

        // ── Alert types ───────────────────────────────────────────────────────
        builder.Property(np => np.LowStockAlerts)
            .HasColumnName("low_stock_alerts")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(np => np.OrderStatusAlerts)
            .HasColumnName("order_status_alerts")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(np => np.SubscriptionAlerts)
            .HasColumnName("subscription_alerts")
            .IsRequired()
            .HasDefaultValue(true);

        // ── Delivery channels ─────────────────────────────────────────────────
        builder.Property(np => np.EmailNotifications)
            .HasColumnName("email_notifications")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(np => np.InAppNotifications)
            .HasColumnName("in_app_notifications")
            .IsRequired()
            .HasDefaultValue(true);

        // ── Audit columns (from BaseEntity) ───────────────────────────────────
        builder.Property(np => np.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(np => np.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(np => np.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // ── Relationship ──────────────────────────────────────────────────────
        builder.HasOne<RetailerAccount>()
            .WithOne()
            .HasForeignKey<NotificationPreference>(np => np.RetailerId)
            .HasConstraintName("fk_notification_preferences_retailer_accounts")
            .OnDelete(DeleteBehavior.Cascade);
    }
}