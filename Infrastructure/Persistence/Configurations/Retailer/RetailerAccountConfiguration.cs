// src/Infrastructure/Persistence/Configurations/RetailerAccountConfiguration.cs
namespace Infrastructure.Persistence.Configurations.Retailer;

/// <summary>
/// EF Core Fluent API configuration for <see cref="RetailerAccount"/>.
///
/// Applies to table: retailer_accounts (snake_case via global UseSnakeCaseNamingConvention).
/// </summary>
public sealed class RetailerAccountConfiguration : IEntityTypeConfiguration<RetailerAccount>
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

        // ── FIX F-03: Optimistic concurrency via PostgreSQL xmin system column ─
        //
        // xmin is a hidden uint column on every PostgreSQL row that is incremented
        // on every UPDATE to that row. EF Core reads it back after each update and
        // uses it as the expected value on the next update's WHERE clause:
        //
        //   UPDATE retailer_accounts SET ... WHERE id = @id AND xmin = @expected_xmin
        //
        // If two concurrent RefreshToken requests both read xmin = 100 and one
        // commits first (xmin becomes 101), the second update finds 0 rows affected
        // and EF Core throws DbUpdateConcurrencyException, which RefreshTokenCommandHandler
        // catches and converts to a 401 "concurrent refresh detected" response.
        //
        // Requires Npgsql.EntityFrameworkCore.PostgreSQL — no migration needed,
        // xmin is a system column that already exists on every table.

        // ── Column constraints ────────────────────────────────────────────────
        builder.Property(r => r.FullName)
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(r => r.Email)
               .HasMaxLength(256)
               .IsRequired();

        builder.Property(r => r.PasswordHash)
               .HasMaxLength(255)
               .IsRequired();

        builder.Property(r => r.BrandName)
               .HasMaxLength(150)
               .IsRequired();

        builder.Property(r => r.BusinessType)
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(r => r.BrandLogoUrl)
               .HasMaxLength(2048);

        builder.Property(r => r.GoogleId)
               .HasMaxLength(128);

        builder.Property(r => r.AccountStatus)
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(r => r.RefreshTokenHash)
               .HasMaxLength(255);

        builder.Property(r => r.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20)
            .IsRequired(false);

        builder.Property(r => r.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(500)
            .IsRequired(false);

        // IsRememberMeSession is a plain bool — no special mapping needed.
        // Defaults to false at DB level (column default).
        builder.Property(r => r.IsRememberMeSession)
               .HasDefaultValue(false);

        // ── Soft-delete global query filter ───────────────────────────────────
        builder.HasQueryFilter(r => !r.IsDeleted);

        // ── Partial unique index on Email (active accounts only) ──────────────
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
        builder.HasIndex(r => r.GoogleId)
               .HasFilter("google_id IS NOT NULL")
               .HasDatabaseName("ix_retailer_accounts_google_id");

        // ── Navigation: NotificationPreference (1:1) ──────────────────────────
        builder.HasOne<NotificationPreference>()
               .WithOne()
               .HasForeignKey<NotificationPreference>(n => n.RetailerId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}