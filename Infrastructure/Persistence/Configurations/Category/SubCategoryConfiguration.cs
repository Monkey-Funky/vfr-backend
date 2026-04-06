namespace Infrastructure.Persistence.Configurations.Category;

/// <summary>
/// EF Core fluent configuration for the <see cref="SubCategory"/> entity.
/// Maps to the <c>sub_categories</c> table.
///
/// IMPORTANT: <see cref="BaseEntity"/> exposes <c>CreatedBy</c> and
/// <c>UpdatedBy</c> properties, but the <c>sub_categories</c> DB table does NOT
/// have these columns (see §B.5 of 02-DatabaseSchema.md). They are explicitly
/// ignored here so EF Core does not attempt to map or migrate them.
/// </summary>
public sealed class SubCategoryConfiguration : IEntityTypeConfiguration<SubCategory>
{
    public void Configure(EntityTypeBuilder<SubCategory> builder)
    {
        // ── Table + Check Constraint ──────────────────────────────────────────
        builder.ToTable("sub_categories", t =>
            t.HasCheckConstraint(
                "ck_sub_categories_status",
                "status IN ('Active', 'Inactive')"));

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(sc => sc.Id);

        builder.Property(sc => sc.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // ── Columns ───────────────────────────────────────────────────────────

        builder.Property(sc => sc.CategoryId)
            .HasColumnName("category_id")
            .IsRequired();

        builder.Property(sc => sc.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(sc => sc.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(150)")
            .IsRequired();

        builder.Property(sc => sc.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasDefaultValue(SubCategory.SubCategoryStatus.Active);

        builder.Property(sc => sc.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(sc => sc.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(sc => sc.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        // ── Ignored Properties ────────────────────────────────────────────────
        // sub_categories table does NOT have created_by / updated_by columns (§B.5).
        builder.Ignore(sc => sc.CreatedBy);
        builder.Ignore(sc => sc.UpdatedBy);

        // ── Partial Unique Index — name uniqueness within parent category ──────
        builder.HasIndex(sc => new { sc.CategoryId, sc.Name })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_sub_categories_category_id_name_active");

        // ── FK Indexes ────────────────────────────────────────────────────────
        builder.HasIndex(sc => sc.CategoryId)
            .HasDatabaseName("ix_sub_categories_category_id");

        builder.HasIndex(sc => sc.RetailerId)
            .HasDatabaseName("ix_sub_categories_retailer_id");

        // ── Soft-Delete Global Query Filter ───────────────────────────────────
        builder.HasQueryFilter(sc => !sc.IsDeleted);

        builder.HasOne<Domain.Entities.Retailer.Category>()
            .WithMany(c => c.SubCategories)
            .HasForeignKey(sc => sc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(sc => sc.RetailerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}