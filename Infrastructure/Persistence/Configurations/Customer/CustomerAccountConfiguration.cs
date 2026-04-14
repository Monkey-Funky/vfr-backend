using Domain.Entities.Customer;
using Domain.Enums.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

/// <summary>
/// EF Core Fluent API configuration for <see cref="CustomerAccount"/>.
/// Table: customer_accounts
/// </summary>
public sealed class CustomerAccountConfiguration : IEntityTypeConfiguration<CustomerAccount>
{
    public void Configure(EntityTypeBuilder<CustomerAccount> builder)
    {
        builder.ToTable("customer_accounts", t =>
        {
            t.HasCheckConstraint("ck_customer_accounts_status",
                "status IN ('Active','PendingEmailVerification','Suspended','PendingDeletion')");
            t.HasCheckConstraint("ck_customer_accounts_gender",
                "gender IN ('Male','Female','Other','PreferNotToSay')");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Email)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.PasswordHash)
            .HasMaxLength(200);

        builder.Property(c => c.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(c => c.Gender)
            .HasMaxLength(20);

        builder.Property(c => c.GoogleId)
            .HasMaxLength(200);

        builder.Property(c => c.RefreshTokenHash)
            .HasMaxLength(200);

        builder.Property(c => c.Status)
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(CustomerStatus.Active);

        // Soft-delete global query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Partial unique index: one email per non-deleted account
        builder.HasIndex(c => c.Email)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("uq_customer_accounts_email");

        // Google ID index
        builder.HasIndex(c => c.GoogleId)
            .HasFilter("google_id IS NOT NULL")
            .HasDatabaseName("idx_customer_accounts_google_id");
    }
}
