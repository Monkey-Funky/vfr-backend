
using Domain.Enums;

namespace Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // ── Table ─────────────────────────────────────────────────────────────
        builder.ToTable("products");

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        // ── Scalar Columns ────────────────────────────────────────────────────
        builder.Property(p => p.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(p => p.CategoryId)
            .HasColumnName("category_id");

        builder.Property(p => p.SubCategoryId)
            .HasColumnName("sub_category_id");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasColumnType("text");

        builder.Property(p => p.Price)
            .HasColumnName("price")
            .HasColumnType("numeric(18,2)");

        builder.Property(p => p.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("EGP");

        builder.Property(p => p.Barcode)
            .HasColumnName("barcode")
            .HasMaxLength(100);

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(ProductStatus.Draft);

        // search_vector is NOT mapped here — see class-level summary above.

        // ── Audit Columns ──────────────────────────────────────────────────────
        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(p => p.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);

        builder.Property(p => p.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);

        builder.Property(p => p.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // ── Global Query Filter (soft-delete) ─────────────────────────────────
        builder.HasQueryFilter(p => !p.IsDeleted);

        // ── Foreign Keys ──────────────────────────────────────────────────────
        builder.HasOne<Domain.Entities.Retailer.RetailerAccount>()
            .WithMany()
            .HasForeignKey(p => p.RetailerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<SubCategory>()
            .WithMany()
            .HasForeignKey(p => p.SubCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // ── Navigation: Images ────────────────────────────────────────────────
        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────────

        // Partial unique index: unique product name per retailer (non-deleted only)
        builder.HasIndex(p => new { p.RetailerId, p.Name })
            .HasFilter("is_deleted = false")
            .IsUnique()
            .HasDatabaseName("uidx_products_retailer_name");

        builder.HasIndex(p => p.RetailerId)
            .HasDatabaseName("idx_products_retailer_id");

        builder.HasIndex(p => p.CategoryId)
            .HasDatabaseName("idx_products_category_id");

        builder.HasIndex(p => new { p.RetailerId, p.Status })
            .HasDatabaseName("idx_products_retailer_status");

        // GIN index on search_vector is created via raw SQL in the migration.
        // It cannot be declared here because the property is not mapped to EF.

        // ── CHECK Constraint ──────────────────────────────────────────────────
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_products_status",
            "status IN ('Active', 'Inactive', 'Draft')"));
    }
}