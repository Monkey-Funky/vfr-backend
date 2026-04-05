
namespace Infrastructure.Persistence.Configurations;

public sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("offers");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(o => o.RetailerId).HasColumnName("retailer_id").IsRequired();
        builder.Property(o => o.CategoryId).HasColumnName("category_id").IsRequired(false);
        builder.Property(o => o.Status).HasColumnName("status").HasColumnType("varchar(20)").IsRequired();
        builder.Property(o => o.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired(false);
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Ignore(o => o.CreatedBy);
        builder.Ignore(o => o.UpdatedBy);
        builder.HasQueryFilter(o => !o.IsDeleted);
        builder.HasIndex(o => o.RetailerId).HasDatabaseName("ix_offers_retailer_id");
        builder.HasIndex(o => o.CategoryId).HasDatabaseName("ix_offers_category_id");
    }
}
