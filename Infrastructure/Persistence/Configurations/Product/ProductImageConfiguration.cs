namespace Infrastructure.Persistence.Configurations.Product;


public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("product_images");

        // ProductImage does NOT extend BaseEntity — configure all columns manually.
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(i => i.ImageUrl)
            .HasColumnName("image_url")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(i => i.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(i => i.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // ── Global Query Filter ───────────────────────────────────────────────
        builder.HasQueryFilter(i => !i.IsDeleted);

        // ── Foreign Key ───────────────────────────────────────────────────────
        builder.HasOne<Domain.Entities.Retailer.Product>()
            .WithMany(p => p.Images)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(i => i.ProductId)
            .HasDatabaseName("idx_product_images_product_id");
    }
}