using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

internal sealed class WardrobeCollectionItemConfiguration : IEntityTypeConfiguration<WardrobeCollectionItem>
{
    public void Configure(EntityTypeBuilder<WardrobeCollectionItem> builder)
    {
        builder.ToTable("wardrobe_collection_items");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.CollectionId)
            .IsRequired()
            .HasColumnName("collection_id");

        builder.Property(i => i.FavoriteId)
            .IsRequired()
            .HasColumnName("favorite_id");

        builder.Property(i => i.CreatedAt)
            .IsRequired()
            .HasColumnName("added_at")
            .HasDefaultValueSql("now()");

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(i => i.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnName("is_deleted");

        // Foreign Keys
        builder.HasOne<WardrobeCollection>()
            .WithMany()
            .HasForeignKey(i => i.CollectionId)
            .HasConstraintName("fk_wardrobe_collection_items_collections")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CustomerFavorite>()
            .WithMany()
            .HasForeignKey(i => i.FavoriteId)
            .HasConstraintName("fk_wardrobe_collection_items_favorites")
            .OnDelete(DeleteBehavior.Cascade);

        // Partial unique index (collection_id, favorite_id) WHERE is_deleted = false
        builder.HasIndex(i => new { i.CollectionId, i.FavoriteId })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_wardrobe_collection_items_collection_favorite_unique");

        // Global query filter for soft deletes
        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
