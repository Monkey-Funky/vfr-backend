using Domain.Enums.Product;

namespace Infrastructure.Persistence.Configurations.Retailer;

/// <summary>
/// EF Core configuration for InventoryRecord.
///
/// KEY DECISIONS:
///   • RowVersion is mapped as IsConcurrencyToken() — NOT as IsRowVersion() (timestamp type).
///     PostgreSQL does not support the SQL Server rowversion/timestamp type.
///     The integer column is incremented manually in domain logic; EF Core includes
///     it in the UPDATE WHERE clause automatically when IsConcurrencyToken() is set.
///   • Global query filter excludes soft-deleted records from ALL queries
///     (GetInventoryQuery, GetInventoryByProductIdQuery, CSV export).
///   • Partial UNIQUE index enforces one InventoryRecord per (retailer_id, product_id)
///     for non-deleted records.
///   • CheckConstraint for current_stock >= 0 provides a DB-level safety net
///     complementing the domain-level validation.
/// </summary>
public sealed class InventoryRecordConfiguration : IEntityTypeConfiguration<InventoryRecord>
{
    public void Configure(EntityTypeBuilder<InventoryRecord> builder)
    {
        // ── Table + Check Constraints (EF Core 9 pattern) ──────────────────────
        builder.ToTable("inventory_records", t =>
        {
            t.HasCheckConstraint(
                "ck_inventory_records_current_stock_non_negative",
                "current_stock >= 0");

            t.HasCheckConstraint(
                "ck_inventory_records_status",
                "status IN ('InStock','LowStock','OutOfStock')");
        });

        // ── Primary Key ────────────────────────────────────────────────────────
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasColumnName("id")
            .IsRequired();

        // ── Tenant FK ─────────────────────────────────────────────────────────
        builder.Property(r => r.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        // ── Product FK ────────────────────────────────────────────────────────
        builder.Property(r => r.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        // ── Snapshot ──────────────────────────────────────────────────────────
        builder.Property(r => r.ProductName)
            .HasColumnName("product_name")
            .HasMaxLength(200)
            .IsRequired();

        // ── Stock Columns ─────────────────────────────────────────────────────
        builder.Property(r => r.CurrentStock)
            .HasColumnName("current_stock")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.SoldQuantity)
            .HasColumnName("sold_quantity")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(r => r.LowStockThreshold)
            .HasColumnName("low_stock_threshold")
            .HasDefaultValue(10)
            .IsRequired();

        // ── Optimistic Concurrency Token ───────────────────────────────────────
        // IsConcurrencyToken() — EF Core will include this column in
        // "UPDATE ... WHERE id = @id AND row_version = @original_row_version".
        // DbUpdateConcurrencyException is thrown if the WHERE clause matches 0 rows.
        builder.Property(r => r.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken()
            .HasDefaultValue(0)
            .IsRequired();

        // ── Status ────────────────────────────────────────────────────────────
        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("InStock")
            .IsRequired();

        // ── Audit Columns from BaseEntity ─────────────────────────────────────
        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(r => r.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        // inventory_records does NOT have created_by / updated_by columns
        builder.Ignore(r => r.CreatedBy);
        builder.Ignore(r => r.UpdatedBy);

        // ── Global Query Filter ────────────────────────────────────────────────
        builder.HasQueryFilter(r => !r.IsDeleted);

        // ── Foreign Keys ──────────────────────────────────────────────────────
        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(r => r.RetailerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Entities.Retailer.Product>()
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Navigation: StockAdjustments ──────────────────────────────────────
        // Defined on StockAdjustmentConfiguration — no explicit nav property on
        // InventoryRecord (audit trail is append-only, read via direct queries).

        // ── Indexes ───────────────────────────────────────────────────────────

        // FK index: retailer_id (enables fast tenant-scoped queries)
        builder.HasIndex(r => r.RetailerId)
            .HasDatabaseName("idx_inventory_records_retailer_id");

        // FK index: product_id
        builder.HasIndex(r => r.ProductId)
            .HasDatabaseName("idx_inventory_records_product_id");

        // Partial UNIQUE: one active inventory record per (retailer, product)
        builder.HasIndex(r => new { r.RetailerId, r.ProductId })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("uq_inventory_records_retailer_product");

        // Composite index for paginated list queries sorted by sold_quantity
        builder.HasIndex(r => new { r.RetailerId, r.SoldQuantity })
            .HasDatabaseName("idx_inventory_records_retailer_sold_qty");
    }
}