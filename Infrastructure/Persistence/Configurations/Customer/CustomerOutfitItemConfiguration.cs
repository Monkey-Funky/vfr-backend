using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

internal sealed class CustomerOutfitItemConfiguration : IEntityTypeConfiguration<CustomerOutfitItem>
{
    public void Configure(EntityTypeBuilder<CustomerOutfitItem> builder)
    {
        builder.ToTable("customer_outfit_items");

        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.OutfitId)
            .HasColumnName("outfit_id")
            .IsRequired();

        builder.Property(e => e.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(e => e.SlotType)
            .HasColumnName("slot")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0)
            .IsRequired();
            
        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasQueryFilter(e => !e.IsDeleted);

        // DATA INTEGRITY MANDATE: OnDelete(Cascade) to Products
        builder.HasOne<Domain.Entities.Retailer.Product>()
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Partial unique index to enforce one product per slot per outfit
        builder.HasIndex(e => new { e.OutfitId, e.SlotType, e.ProductId })
            .IsUnique()
            .HasFilter("is_deleted = false");
    }
}
