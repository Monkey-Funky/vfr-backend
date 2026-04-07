using Domain.Entities.Analytics;

namespace Infrastructure.Persistence.Configurations.Dashboard;

public sealed class ReturnReasonConfiguration
    : IEntityTypeConfiguration<ReturnReason>
{
    public void Configure(EntityTypeBuilder<ReturnReason> builder)
    {
        builder.ToTable("return_reasons");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.RetailerId).IsRequired();
        builder.Property(r => r.OrderItemId);
        builder.Property(r => r.ProductId);

        builder.Property(r => r.Reason)
               .HasMaxLength(50)
               .HasConversion<string>()
               .IsRequired();

        builder.HasCheckConstraint(
            "ck_return_reasons_reason",
            "reason IN ('WrongSize','DefectivItem','NotAsDescribed','ChangedMind','LateDelivery','DamagedInShipping','Other')");

        builder.Property(r => r.ReturnedAt)
               .HasColumnType("timestamptz")
               .HasDefaultValueSql("now()")
               .IsRequired();

        builder.HasIndex(r => r.RetailerId)
               .HasDatabaseName("idx_return_reasons_retailer_id");

        builder.HasIndex(r => new { r.RetailerId, r.ReturnedAt })
               .HasDatabaseName("idx_return_reasons_retailer_returnedat");

        builder.HasIndex(r => r.ProductId)
               .HasDatabaseName("idx_return_reasons_product_id");
    }
}
