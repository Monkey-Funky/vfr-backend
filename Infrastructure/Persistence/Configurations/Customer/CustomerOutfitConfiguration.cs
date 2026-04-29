using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

internal sealed class CustomerOutfitConfiguration : IEntityTypeConfiguration<CustomerOutfit>
{
    public void Configure(EntityTypeBuilder<CustomerOutfit> builder)
    {
        builder.ToTable("customer_outfits");

        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(e => e.StyleCategory)
            .HasColumnName("style")
            .HasMaxLength(50);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(e => e.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        // Foreign key to CustomerAccount (Assuming customer_accounts exists, but we just map the column)
        builder.HasOne<CustomerAccount>()
            .WithMany()
            .HasForeignKey(e => e.CustomerId)
            .HasConstraintName("fk_customer_outfits_customer_accounts")
            .OnDelete(DeleteBehavior.Cascade);

        // Global query filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        builder.HasMany(e => e.Items)
            .WithOne()
            .HasForeignKey(e => e.OutfitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
