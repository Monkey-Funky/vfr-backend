namespace Infrastructure.Persistence.Configurations.Retailer;

/// <summary>
/// EF Core Fluent API configuration for <see cref="RetailerAccount"/>.
///
/// Applies to table: retailer_accounts (snake_case via global UseSnakeCaseNamingConvention).
/// </summary>
public sealed class RetailerAccountConfiguration
    : IEntityTypeConfiguration<RetailerAccount>
{
    public void Configure(EntityTypeBuilder<RetailerAccount> builder)
    {
        // ── Table + check constraint ──────────────────────────────────────────
        builder.ToTable("retailer_accounts", t =>
        {
            t.HasCheckConstraint(
                "chk_retailer_accounts_status",
                "account_status IN ('Active','PendingEmailVerification','Suspended','PendingDeletion')");
        });

        // ── Primary Key ───────────────────────────────────────────────────────
        builder.HasKey(r => r.Id);

        // ── Column constraints — Identity ─────────────────────────────────────
        builder.Property(r => r.FullName)
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(r => r.Email)
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(r => r.PasswordHash)
               .HasMaxLength(255)
               .IsRequired();

        // ── Column constraints — Brand / Business ─────────────────────────────
        builder.Property(r => r.BrandName)
               .HasMaxLength(150)
               .IsRequired();

        builder.Property(r => r.BusinessType)
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(r => r.BrandLogoUrl)
               .HasMaxLength(2048);

        // ── Column constraints — OAuth ────────────────────────────────────────
        builder.Property(r => r.GoogleId)
               .HasMaxLength(128);

        // ── Column constraints — Account Status ───────────────────────────────
        builder.Property(r => r.AccountStatus)
               .HasMaxLength(50)
               .IsRequired();

        // ── Column constraints — Refresh Token ────────────────────────────────
        builder.Property(r => r.RefreshTokenHash)
               .HasMaxLength(255);

        // IsRememberMeSession defaults to false at DB level.
        builder.Property(r => r.IsRememberMeSession)
               .HasDefaultValue(false);

        // ── FIX BUG-005: AvailableBalance — explicit precision and default ─────
        //
        // RetailerAccount.AvailableBalance has the doc comment:
        //   "DB column: available_balance numeric(18,2) NOT NULL DEFAULT 0"
        //
        // Without HasPrecision, EF Core may generate a column type different from
        // numeric(18,2). Without HasDefaultValue, the DB-level DEFAULT 0 is absent,
        // which can cause issues with rows that bypass EF Core (e.g. raw SQL inserts
        // or seeding scripts).
        //
        // NOTE: Because this property was absent from the original configuration,
        // the existing migration does NOT include this column. After applying this fix,
        // run:
        //   dotnet ef migrations add Add_AvailableBalance_To_RetailerAccounts \
        //     --project src/Infrastructure \
        //     --startup-project src/API
        // to generate a migration that adds the column with the correct type and default.
        builder.Property(r => r.AvailableBalance)
               .HasPrecision(18, 2)
               .HasDefaultValue(0m)
               .IsRequired();

        // ── Soft-delete global query filter ───────────────────────────────────
        //
        // All queries on RetailerAccount automatically exclude soft-deleted rows.
        // Use .IgnoreQueryFilters() only for administrative or audit operations.
        builder.HasQueryFilter(r => !r.IsDeleted);

        // ── Partial unique index on Email (active accounts only) ──────────────
        //
        // Allows a deleted retailer's email to be re-registered by a new account.
        builder.HasIndex(r => r.Email)
               .IsUnique()
               .HasFilter("is_deleted = false")
               .HasDatabaseName("ux_retailer_accounts_email_active");

        // ── Partial unique index on BrandName (active accounts only) ──────────
        builder.HasIndex(r => r.BrandName)
               .IsUnique()
               .HasFilter("is_deleted = false")
               .HasDatabaseName("ux_retailer_accounts_brand_name_active");

        // ── Sparse index on GoogleId ──────────────────────────────────────────
        //
        // NULL-filtered so that password-only accounts (GoogleId IS NULL) are not
        // indexed, saving index space and avoiding a unique constraint on NULLs.
        builder.HasIndex(r => r.GoogleId)
               .HasFilter("google_id IS NOT NULL")
               .HasDatabaseName("ix_retailer_accounts_google_id");

        // ── Navigation: NotificationPreference (1:1) ──────────────────────────
        //
        // Cascade delete ensures the preference row is cleaned up when the
        // retailer account is hard-deleted (soft-delete doesn't trigger this).
        builder.HasOne<NotificationPreference>()
               .WithOne()
               .HasForeignKey<NotificationPreference>(n => n.RetailerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}