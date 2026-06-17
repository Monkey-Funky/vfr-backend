using Domain.Enums.Product;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Configurations.Product;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Domain.Entities.Retailer.Product>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Retailer.Product> builder)
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

        // ── AI / Style-Recommendation External Identifier ─────────────────────
        //
        // DESIGN: model_id is the external string ID used by the AI style-recommendation
        //   model (e.g. "78_y3ppkj"). It corresponds to the "ID for Image" column in
        //   Products_Data-2.xlsx and is derived from the Cloudinary image filename.
        //
        //   • NULL for all retailer-created products — only populated by ExcelDataSeeder.
        //   • Unique partial index WHERE model_id IS NOT NULL prevents duplicate model
        //     IDs across different seeder runs while allowing any number of NULL values.
        //   • Max 50 chars — the longest known model ID is well under this limit.
        //   • Never set via product create/update flows — immutable after seeding.
        builder.Property(p => p.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(50)
            .IsRequired(false); // nullable

        // ── Search Vector (Shadow Property — PostgreSQL GENERATED ALWAYS AS STORED) ──
        //
        // DESIGN: The Product entity has NO SearchVector C# property.
        //   Using a shadow property keeps the Domain layer free of Npgsql dependencies.
        //   NpgsqlTsVector is available here in the Infrastructure layer.
        //
        // EF Core behaviour with HasComputedColumnSql + stored: true:
        //   • EF Core will NEVER include this column in INSERT or UPDATE statements.
        //   • PostgreSQL maintains the value automatically on every row write.
        //   • The shadow property allows HasIndex("SearchVector").HasMethod("gin")
        //     to declare the GIN index without a C# property expression.
        //
        // FTS queries in ProductRepository reference this column via
        //   EF.Property<NpgsqlTsVector>(p, "SearchVector").Matches(...)
        // which translates to: search_vector @@ plainto_tsquery('english', ?)
        //
        // ⚠ MIGRATION: The migration scaffold MUST NOT include search_vector in
        //   CreateTable. Add it via ALTER TABLE in the migration's Up() raw SQL block.
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('english', " +
                "coalesce(name, '') || ' ' || " +
                "coalesce(description, '') || ' ' || " +
                "coalesce(barcode, ''))",
                stored: true);

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
        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(p => p.RetailerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Entities.Retailer.Category>()
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

        builder.Navigation(p => p.Images)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Indexes ───────────────────────────────────────────────────────────

        // Partial unique: unique product name per retailer (non-deleted only)
        builder.HasIndex(p => new { p.RetailerId, p.Name })
            .HasFilter("is_deleted = false")
            .IsUnique()
            .HasDatabaseName("uidx_products_retailer_name");

        // Partial unique: barcode uniqueness scoped per retailer (not global)
        builder.HasIndex(p => new { p.RetailerId, p.Barcode })
            .HasFilter("is_deleted = false AND barcode IS NOT NULL")
            .IsUnique()
            .HasDatabaseName("uidx_products_retailer_barcode");

        builder.HasIndex(p => p.RetailerId)
            .HasDatabaseName("idx_products_retailer_id");

        builder.HasIndex(p => p.CategoryId)
            .HasDatabaseName("idx_products_category_id");

        builder.HasIndex(p => p.SubCategoryId)
            .HasDatabaseName("idx_products_sub_category_id");

        // Composite index: (retailer_id, status) for status-filtered list queries
        builder.HasIndex(p => new { p.RetailerId, p.Status })
            .HasDatabaseName("idx_products_retailer_status");

        // Composite index: (retailer_id, created_at DESC) for default sort
        builder.HasIndex(p => new { p.RetailerId, p.CreatedAt })
            .HasDatabaseName("idx_products_retailer_created_at");

        // GIN index on the generated tsvector shadow property.
        // Npgsql translates .HasMethod("gin") → USING GIN in the migration DDL.
        // This makes plainto_tsquery FTS searches O(log n) instead of O(n).
        builder.HasIndex("SearchVector")
            .HasMethod("gin")
            .HasDatabaseName("idx_products_search_vector");

        // Partial unique index on model_id.
        // NULL values are intentionally excluded — retailer products have NULL model_id.
        // This allows infinite retailer products (all NULL) while enforcing uniqueness
        // across the seeded AI catalogue products.
        builder.HasIndex(p => p.ModelId)
            .HasFilter("model_id IS NOT NULL")
            .IsUnique()
            .HasDatabaseName("uidx_products_model_id");

        // ── CHECK Constraint ──────────────────────────────────────────────────
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_products_status",
            "status IN ('Active', 'Inactive', 'Draft', 'OutOfStock')"));
    }
}