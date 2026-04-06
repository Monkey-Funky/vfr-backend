

namespace Infrastructure.Persistence.Configurations.Orders;


/// <summary>
/// EF Core configuration for StockAdjustment.
///
/// FIXES APPLIED:
///   • StockAdjustment now extends BaseEntity. This configuration maps
///     BaseEntity fields to their actual DB column names and Ignores
///     the ones that don't exist in the stock_adjustments table.
///   • BaseEntity.CreatedAt maps to the "adjusted_at" column.
///   • HasCheckConstraint moved inside ToTable(t => ...) — fixes CS0618.
/// </summary>
public sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        // ✅ EF Core 9 pattern
        builder.ToTable("stock_adjustments", t =>
        {
            t.HasCheckConstraint(
                "ck_stock_adjustments_type",
                "adjustment_type IN ('ManualIncrease','ManualDecrease','OrderSale','ReturnRestock')");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(s => s.InventoryRecordId)
            .HasColumnName("inventory_record_id")
            .IsRequired();

        builder.Property(s => s.AdjustmentType)
            .HasColumnName("adjustment_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.OldQuantity)
            .HasColumnName("old_quantity")
            .IsRequired();

        builder.Property(s => s.NewQuantity)
            .HasColumnName("new_quantity")
            .IsRequired();

        builder.Property(s => s.Reason)
            .HasColumnName("reason");

        builder.Property(s => s.AdjustedById)
            .HasColumnName("adjusted_by_id")
            .IsRequired();

        // ✅ BaseEntity.CreatedAt maps to the "adjusted_at" column in the DB
        builder.Property(s => s.CreatedAt)
            .HasColumnName("adjusted_at")
            .IsRequired();

        // stock_adjustments has no updated_at, created_by, updated_by, or is_deleted columns
        builder.Ignore(s => s.UpdatedAt);
        builder.Ignore(s => s.CreatedBy);
        builder.Ignore(s => s.UpdatedBy);
        builder.Ignore(s => s.IsDeleted);  // audit records are immutable — never soft-deleted

        // No global query filter — adjustments are never soft-deleted

        // FK: stock_adjustments.inventory_record_id → inventory_records.id
        builder.HasOne<InventoryRecord>()
            .WithMany()
            .HasForeignKey(s => s.InventoryRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: stock_adjustments.adjusted_by_id → retailer_accounts.id
        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(s => s.AdjustedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.InventoryRecordId)
            .HasDatabaseName("idx_stock_adjustments_inventory_record_id");

        builder.HasIndex(s => s.AdjustedById)
            .HasDatabaseName("idx_stock_adjustments_adjusted_by_id");
    }
}