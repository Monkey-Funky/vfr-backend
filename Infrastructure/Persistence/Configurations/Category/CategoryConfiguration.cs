namespace Infrastructure.Persistence.Configurations.Category;

/// <summary>
/// EF Core fluent configuration for the <see cref="Category"/> entity.
/// Maps to the <c>categories</c> table (snake_case applied globally via UseSnakeCaseNamingConvention).
/// </summary>
public sealed class CategoryConfiguration : IEntityTypeConfiguration<Domain.Entities.Retailer.Category>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Retailer.Category> builder)
    {
        // ── Table + Check Constraint ──────────────────────────────────────────
        builder.ToTable("categories", t =>
            t.HasCheckConstraint(
                "ck_categories_status",
                "status IN ('Active', 'Inactive')"));

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        // ── Columns ───────────────────────────────────────────────────────────

        builder.Property(c => c.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasColumnType("varchar(150)")
            .IsRequired();

        builder.Property(c => c.Description)
            .HasColumnName("description")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(c => c.CoverImageUrl)
            .HasColumnName("cover_image_url")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasColumnType("varchar(20)")
            .IsRequired()
            .HasDefaultValue(Domain.Entities.Retailer.Category.CategoryStatus.Active);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(c => c.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        // ── Partial Unique Index — name uniqueness per retailer (soft-delete aware) ──
        builder.HasIndex(c => new { c.RetailerId, c.Name })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_categories_retailer_id_name_active");

        // ── FK Index (PostgreSQL does not auto-create indexes on FKs) ─────────
        builder.HasIndex(c => c.RetailerId)
            .HasDatabaseName("ix_categories_retailer_id");

        // ── Soft-Delete Global Query Filter ───────────────────────────────────
        builder.HasQueryFilter(c => !c.IsDeleted);

        // ── Navigation backing-field declaration ──────────────────────────────
        builder.Navigation(c => c.SubCategories)
            .HasField("_subCategories")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Relationship — Category → SubCategories ───────────────────────────
        builder.HasMany(c => c.SubCategories)
            .WithOne()
            .HasForeignKey(sc => sc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Foreign Key to RetailerAccount ────────────────────────────────────
        builder.HasOne<Domain.Entities.Retailer.RetailerAccount>()
            .WithMany()
            .HasForeignKey(c => c.RetailerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}