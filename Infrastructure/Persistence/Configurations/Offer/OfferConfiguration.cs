namespace Infrastructure.Persistence.Configurations.Offer;

/// <summary>
/// EF Core fluent configuration for the Offer entity.
/// Maps to the `offers` table as defined in 02-DatabaseSchema.md §B.8.
/// Includes all CHECK constraints, indexes, and FK relationships.
/// </summary>
public sealed class OfferConfiguration : IEntityTypeConfiguration<Domain.Entities.Retailer.Offer>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Retailer.Offer> builder)
    {
        // ✅ EF Core 9 pattern: CHECK constraints inside ToTable(t => { ... })
        builder.ToTable("offers", t =>
        {
            t.HasCheckConstraint(
                "ck_offers_offer_type",
                "offer_type IN ('Product', 'Category')");

            t.HasCheckConstraint(
                "ck_offers_discount_type",
                "discount_type IN ('Percentage', 'Fixed')");

            t.HasCheckConstraint(
                "ck_offers_status",
                "status IN ('Active', 'Inactive', 'Expired')");

            // Mutual exclusivity: ProductId XOR CategoryId based on OfferType
            t.HasCheckConstraint(
                "ck_offers_type_target_mutual_exclusivity",
                "(offer_type = 'Product'   AND product_id  IS NOT NULL AND category_id IS NULL) " +
                "OR " +
                "(offer_type = 'Category'  AND category_id IS NOT NULL AND product_id  IS NULL)");
        });

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
               .HasColumnName("id")
               .HasColumnType("uuid")
               .HasDefaultValueSql("gen_random_uuid()");

        // ── Properties ────────────────────────────────────────────────────────
        builder.Property(o => o.RetailerId)
               .HasColumnName("retailer_id")
               .HasColumnType("uuid")
               .IsRequired();

        builder.Property(o => o.Title)
               .HasColumnName("title")
               .HasColumnType("varchar(200)")
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(o => o.Description)
               .HasColumnName("description")
               .HasColumnType("text");

        builder.Property(o => o.OfferType)
               .HasColumnName("offer_type")
               .HasColumnType("varchar(20)")
               .IsRequired();

        builder.Property(o => o.ProductId)
               .HasColumnName("product_id")
               .HasColumnType("uuid");

        builder.Property(o => o.CategoryId)
               .HasColumnName("category_id")
               .HasColumnType("uuid");

        builder.Property(o => o.DiscountType)
               .HasColumnName("discount_type")
               .HasColumnType("varchar(20)")
               .IsRequired();

        builder.Property(o => o.DiscountValue)
               .HasColumnName("discount_value")
               .HasColumnType("numeric(18,2)")
               .IsRequired();

        // DateOnly → PostgreSQL date (Npgsql handles this automatically)
        builder.Property(o => o.StartDate)
               .HasColumnName("start_date")
               .HasColumnType("date")
               .IsRequired();

        builder.Property(o => o.EndDate)
               .HasColumnName("end_date")
               .HasColumnType("date");

        builder.Property(o => o.CoverImageUrl)
               .HasColumnName("cover_image_url")
               .HasColumnType("text")
               .IsRequired();

        builder.Property(o => o.Status)
               .HasColumnName("status")
               .HasColumnType("varchar(20)")
               .IsRequired()
               .HasDefaultValue("Active");

        builder.Property(o => o.CreatedAt)
               .HasColumnName("created_at")
               .HasColumnType("timestamptz")
               .IsRequired()
               .HasDefaultValueSql("now()");

        builder.Property(o => o.UpdatedAt)
               .HasColumnName("updated_at")
               .HasColumnType("timestamptz");

        builder.Property(o => o.IsDeleted)
               .HasColumnName("is_deleted")
               .HasColumnType("boolean")
               .IsRequired()
               .HasDefaultValue(false);

        // Ignore BaseEntity audit fields not in the offers table
        builder.Ignore(o => o.CreatedBy);
        builder.Ignore(o => o.UpdatedBy);

        // IsExpired and IsActive(now) are pure in-memory computed properties — not persisted
        builder.Ignore(o => o.IsExpired);

        // ── Global Query Filter (soft-delete) ─────────────────────────────────
        builder.HasQueryFilter(o => !o.IsDeleted);

        // ── Relationships ─────────────────────────────────────────────────────
        builder.HasOne(o => o.Retailer)
               .WithMany(r => r.Offers)
               .HasForeignKey(o => o.RetailerId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(o => o.Product)
               .WithMany()
               .HasForeignKey(o => o.ProductId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.Category)
               .WithMany()
               .HasForeignKey(o => o.CategoryId)
               .OnDelete(DeleteBehavior.SetNull);

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(o => o.RetailerId)
               .HasDatabaseName("idx_offers_retailer_id");

        builder.HasIndex(o => o.ProductId)
               .HasDatabaseName("idx_offers_product_id");

        builder.HasIndex(o => o.CategoryId)
               .HasDatabaseName("idx_offers_category_id");

        // Composite: retailer-scoped status filter (most common query pattern)
        builder.HasIndex(o => new { o.RetailerId, o.Status })
               .HasDatabaseName("idx_offers_retailer_status");

        // Partial index: used by OfferExpiryJob for expired candidates only
        builder.HasIndex(o => o.EndDate)
               .HasFilter("status = 'Active' AND is_deleted = false")
               .HasDatabaseName("idx_offers_end_date_active");
    }
}