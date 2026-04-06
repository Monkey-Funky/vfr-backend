using Domain.Entities.Orders;
using Domain.Enums.Orders;


namespace Infrastructure.Persistence.Configurations.Orders;
/// <summary>
/// EF Core configuration for the Order entity.
///
/// FIXES APPLIED:
///   • HasCheckConstraint moved inside ToTable(t => ...) — fixes CS0618 obsolete warning.
///   • RowVersion added as IsConcurrencyToken (optimistic lock for concurrent status updates).
///   • IsDeleted declared explicitly to ensure HasQueryFilter works correctly.
///     (Order.cs no longer re-declares it — it inherits from BaseEntity.)
/// </summary>
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // ✅ EF Core 9 pattern: check constraints inside ToTable(t => ...)
        builder.ToTable("orders", t =>
        {
            t.HasCheckConstraint(
                "ck_orders_status",
                "status IN ('NotProcessed','Processing','Shipped','Delivered','Cancelled')");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(o => o.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(o => o.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(o => o.CustomerName)
            .HasColumnName("customer_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.OrderDate)
            .HasColumnName("order_date")
            .IsRequired();

        builder.Property(o => o.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .HasDefaultValue("EGP")
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasDefaultValue("NotProcessed")
            .IsRequired();

        // ✅ Optimistic concurrency token — EF Core includes RowVersion in UPDATE WHERE clause.
        // Prevents two simultaneous Processing→Shipped transitions from both succeeding.
        builder.Property(o => o.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken()
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(o => o.UpdatedAt)
            .HasColumnName("updated_at");

        // IsDeleted is declared in BaseEntity only — never in Order.cs (fixes CS0108 warning).
        builder.Property(o => o.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        // Ignore BaseEntity audit fields that don't exist in the orders table
        builder.Ignore(o => o.CreatedBy);
        builder.Ignore(o => o.UpdatedBy);

        // Global query filter — all queries auto-exclude soft-deleted orders
        builder.HasQueryFilter(o => !o.IsDeleted);

        // Navigation: one Order → many OrderItems (CASCADE delete)
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(o => o.RetailerId)
            .HasDatabaseName("idx_orders_retailer_id");

        builder.HasIndex(o => o.CustomerId)
            .HasDatabaseName("idx_orders_customer_id");

        builder.HasIndex(o => new { o.RetailerId, o.Status })
            .HasDatabaseName("idx_orders_retailer_status");

        builder.HasIndex(o => new { o.RetailerId, o.CreatedAt })
            .HasDatabaseName("idx_orders_retailer_createdat");
    }
}