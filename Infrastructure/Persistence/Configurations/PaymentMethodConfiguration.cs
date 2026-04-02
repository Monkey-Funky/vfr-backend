using Domain.Entities.Retailer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the PaymentMethod entity → payment_methods table (B.13).
///
/// Security Note:
///   cardholder_name_encrypted stores AES-256-CBC ciphertext (Base64 encoded).
///   Decryption is performed in query handlers via IEncryptionService.
///   No EF value converter is used — explicit encryption/decryption ensures
///   the encryption key is never embedded in the EF configuration itself.
///
/// Soft Delete:
///   payment_methods supports is_deleted (soft delete via Repository.SoftDeleteAsync).
/// </summary>
public sealed class PaymentMethodConfiguration
    : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.ToTable("payment_methods");

        // ── Primary Key ────────────────────────────────────────────────────────
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        // ── Columns ───────────────────────────────────────────────────────────
        builder.Property(x => x.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(x => x.ProviderType)
            .HasColumnName("provider_type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.CardholderNameEncrypted)
            .HasColumnName("cardholder_name_encrypted")
            .IsRequired();

        builder.Property(x => x.CardNumberLast4)
            .HasColumnName("card_number_last4")
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(x => x.ExpiryDate)
            .HasColumnName("expiry_date")
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(x => x.StripePaymentMethodId)
            .HasColumnName("stripe_payment_method_id")
            .HasMaxLength(100);

        builder.Property(x => x.IsDefault)
            .HasColumnName("is_default")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsSaved)
            .HasColumnName("is_saved")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired()
            .HasDefaultValue(false);

        // ── Ignore BaseEntity fields absent from B.13 schema ──────────────────
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        // ── CHECK Constraints ─────────────────────────────────────────────────
        builder.HasCheckConstraint(
            "chk_payment_methods_provider_type",
            "provider_type IN ('Visa', 'Mastercard', 'PayPal', 'ApplePay', 'Stripe', 'GooglePay', 'Bitpay')");

        // ── Foreign Keys ──────────────────────────────────────────────────────
        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(x => x.RetailerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_payment_methods_retailer_id");

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(x => x.RetailerId)
            .HasDatabaseName("ix_payment_methods_retailer_id");

        // Partial index — efficiently find the active default card per retailer.
        builder.HasIndex(x => new { x.RetailerId, x.IsDefault })
            .HasFilter("is_deleted = false AND is_default = true")
            .HasDatabaseName("ix_payment_methods_retailer_default");

        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("ix_payment_methods_expires_at");
    }
}