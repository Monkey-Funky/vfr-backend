using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

internal sealed class CustomerFavoriteConfiguration : IEntityTypeConfiguration<CustomerFavorite>
{
    public void Configure(EntityTypeBuilder<CustomerFavorite> builder)
    {
        builder.ToTable("customer_favorites");

        builder.HasKey(f => f.Id);

        // Properties
        builder.Property(f => f.CustomerId)
            .IsRequired()
            .HasColumnName("customer_id");

        builder.Property(f => f.ProductId)
            .IsRequired()
            .HasColumnName("product_id");

        builder.Property(f => f.RetailerId)
            .IsRequired()
            .HasColumnName("retailer_id");

        builder.Property(f => f.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(f => f.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(f => f.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");

        builder.HasOne<CustomerAccount>() 
               .WithMany()
               .HasForeignKey(x => x.CustomerId)
               .HasConstraintName("fk_customer_favorites_customer_accounts")
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Entities.Retailer.Product>() 
               .WithMany()
               .HasForeignKey(x => x.ProductId)
               .HasConstraintName("fk_customer_favorites_products")
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.CreatedAt)
               .HasDefaultValueSql("now()");

        // Indexes
        builder.HasIndex(f => f.CustomerId)
            .HasDatabaseName("ix_customer_favorites_customer_id");

        builder.HasIndex(f => f.ProductId)
            .HasDatabaseName("ix_customer_favorites_product_id");

        builder.HasIndex(f => f.RetailerId)
            .HasDatabaseName("ix_customer_favorites_retailer_id");

        // Partial Unique Index: prevent duplicate active favorites for the same product by the same customer
        builder.HasIndex(f => new { f.CustomerId, f.ProductId })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_customer_favorites_customer_product_unique");

        // Global Query Filter
        builder.HasQueryFilter(f => !f.IsDeleted);
    }
}
