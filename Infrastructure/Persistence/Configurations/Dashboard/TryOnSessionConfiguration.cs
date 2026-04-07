using Domain.Entities.Analytics;

namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class TryOnSessionConfiguration
    : IEntityTypeConfiguration<TryOnSession>
{
    public void Configure(EntityTypeBuilder<TryOnSession> builder)
    {
        builder.ToTable("try_on_sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.RetailerId).IsRequired();
        builder.Property(s => s.ProductId);
        builder.Property(s => s.CustomerId);

        builder.Property(s => s.SessionDurationSeconds)
               .HasDefaultValue(0)
               .IsRequired();

        builder.Property(s => s.ResultedInPurchase)
               .HasDefaultValue(false)
               .IsRequired();

        builder.Property(s => s.CreatedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        builder.HasIndex(s => s.RetailerId)
               .HasDatabaseName("idx_try_on_sessions_retailer_id");

        builder.HasIndex(s => s.ProductId)
               .HasDatabaseName("idx_try_on_sessions_product_id");
    }
}
