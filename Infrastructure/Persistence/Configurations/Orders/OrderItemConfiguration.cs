using Domain.Entities.Orders;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Infrastructure.Persistence.Configurations.Orders;

/// <summary>
/// EF Core configuration for the OrderItem entity.
///
/// FIXES APPLIED:
///   • HasCheckConstraint moved inside ToTable(t => ...) — fixes CS0618 obsolete warning.
///   • BaseEntity fields absent from order_items table are explicitly Ignored.
///   • ProductId FK uses OnDelete(SetNull) — product deletion does not cascade to order history.
/// </summary>
public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        // ✅ EF Core 9 pattern: check constraints inside ToTable(t => ...)
        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(i => i.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(i => i.ProductId)
            .HasColumnName("product_id");  // nullable — product may be deleted

        builder.Property(i => i.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(200)
            .IsRequired();

        // ✅ UnitPrice is a snapshot — private setter in the entity prevents EF change-tracker
        // fixup from overwriting it if the related Product entity is modified in the same context.
        builder.Property(i => i.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.Property(i => i.Total)
            .HasColumnName("total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // order_items does NOT have these columns — ignore them to prevent EF errors
        builder.Ignore(i => i.UpdatedAt);
        builder.Ignore(i => i.CreatedBy);
        builder.Ignore(i => i.UpdatedBy);
        builder.Ignore(i => i.IsDeleted);

        // Snapshot FK: ON DELETE SET NULL — sets product_id to NULL if product is deleted,
        // but leaves ProductName and UnitPrice snapshot values intact.
        builder.HasOne<Domain.Entities.Retailer.Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // Indexes
        builder.HasIndex(i => i.OrderId)
            .HasDatabaseName("idx_order_items_order_id");

        builder.HasIndex(i => i.ProductId)
            .HasDatabaseName("idx_order_items_product_id");
    }
}