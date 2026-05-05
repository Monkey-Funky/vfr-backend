using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

internal sealed class WardrobeCollectionConfiguration : IEntityTypeConfiguration<WardrobeCollection>
{
    public void Configure(EntityTypeBuilder<WardrobeCollection> builder)
    {
        builder.ToTable("wardrobe_collections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerId)
            .IsRequired()
            .HasColumnName("customer_id");

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");

        // Foreign Key
        builder.HasOne<CustomerAccount>()
            .WithMany()
            .HasForeignKey(c => c.CustomerId)
            .HasConstraintName("fk_wardrobe_collections_customer_accounts")
            .OnDelete(DeleteBehavior.Cascade);

        // Partial unique index (customer_id, name) WHERE is_deleted = false
        builder.HasIndex(c => new { c.CustomerId, c.Name })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_wardrobe_collections_customer_id_name_unique");

        // Global query filter for soft deletes
        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
