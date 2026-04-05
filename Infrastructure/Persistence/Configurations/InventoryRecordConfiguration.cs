using Domain.Enums;

namespace Infrastructure.Persistence.Configurations;


public sealed class InventoryRecordConfiguration : IEntityTypeConfiguration<InventoryRecord>
{
    public void Configure(EntityTypeBuilder<InventoryRecord> builder)
    {
        builder.ToTable("inventory_records");

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(ir => ir.Id);
        builder.Property(ir => ir.Id).HasColumnName("id");

        // ── Columns ───────────────────────────────────────────────────────────
        builder.Property(ir => ir.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(ir => ir.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(ir => ir.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ir => ir.CurrentStock)
            .HasColumnName("current_stock")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(ir => ir.SoldQuantity)
            .HasColumnName("sold_quantity")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(ir => ir.LowStockThreshold)
            .HasColumnName("low_stock_threshold")
            .IsRequired()
            .HasDefaultValue(10);

        // ── Optimistic Concurrency Token ──────────────────────────────────────
        // row_version is an integer column incremented on every DB update.
        // EF Core includes "WHERE row_version = @original" in every UPDATE,
        // throwing DbUpdateConcurrencyException on conflict.
        builder.Property(ir => ir.RowVersion)
            .HasColumnName("row_version")
            .IsRequired()
            .HasDefaultValue(0)
            .IsConcurrencyToken();

        builder.Property(ir => ir.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(InventoryStatus.InStock);

        // ── Audit Columns ──────────────────────────────────────────────────────
        builder.Property(ir => ir.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(ir => ir.UpdatedAt)
            .HasColumnName("updated_at");

        // InventoryRecord has no CreatedBy / UpdatedBy — ignore inherited properties
        builder.Ignore(ir => ir.CreatedBy);
        builder.Ignore(ir => ir.UpdatedBy);

        builder.Property(ir => ir.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // ── Global Query Filter (soft-delete) ─────────────────────────────────
        builder.HasQueryFilter(ir => !ir.IsDeleted);

        // ── Foreign Keys ──────────────────────────────────────────────────────
        builder.HasOne<Domain.Entities.Retailer.RetailerAccount>()
            .WithMany()
            .HasForeignKey(ir => ir.RetailerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(ir => ir.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────────
        // One inventory record per product per retailer (non-deleted only)
        builder.HasIndex(ir => new { ir.RetailerId, ir.ProductId })
            .HasFilter("is_deleted = false")
            .IsUnique()
            .HasDatabaseName("uidx_inventory_records_retailer_product");

        builder.HasIndex(ir => ir.RetailerId)
            .HasDatabaseName("idx_inventory_records_retailer_id");

        builder.HasIndex(ir => ir.ProductId)
            .HasDatabaseName("idx_inventory_records_product_id");

        builder.HasIndex(ir => new { ir.RetailerId, ir.Status })
            .HasDatabaseName("idx_inventory_records_low_stock");

        // ── CHECK Constraints ─────────────────────────────────────────────────
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_inventory_records_current_stock_non_negative",
                "current_stock >= 0");

            t.HasCheckConstraint(
                "ck_inventory_records_status",
                "status IN ('InStock', 'LowStock', 'OutOfStock')");
        });
    }
}