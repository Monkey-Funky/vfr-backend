using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

/// <summary>
/// EF Core Fluent API configuration for <see cref="CustomerAddress"/>.
/// Table: customer_addresses
/// </summary>
public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("customer_addresses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Label)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AddressLine1)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.AddressLine2)
            .HasMaxLength(200);

        builder.Property(a => a.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.StateProvince)
            .HasMaxLength(100);

        builder.Property(a => a.PostalCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Country)
            .HasMaxLength(100)
            .IsRequired();

        // Relationship
        builder.HasOne(a => a.CustomerAccount)
            .WithMany()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft-delete global query filter
        builder.HasQueryFilter(a => !a.IsDeleted);

        // FK index
        builder.HasIndex(a => a.CustomerId)
            .HasDatabaseName("idx_customer_addresses_customer_id");

        // Partial unique: max one default address per customer among non-deleted rows
        builder.HasIndex(a => new { a.CustomerId })
            .IsUnique()
            .HasFilter("is_default = true AND is_deleted = false")
            .HasDatabaseName("uq_customer_addresses_one_default");
    }
}
