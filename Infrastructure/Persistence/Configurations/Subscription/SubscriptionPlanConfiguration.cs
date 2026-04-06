using Domain.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Subscription;

public sealed class SubscriptionPlanConfiguration
    : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        // FIX-002: All CHECK constraints inside ToTable
        builder.ToTable("subscription_plans", t =>
        {
            t.HasCheckConstraint(
                "chk_subscription_plans_tier",
                "tier IN ('Basic','Standard','Enterprise','SaaS')");

            t.HasCheckConstraint(
                "chk_subscription_plans_billing_cycle",
                "billing_cycle IN ('Monthly','Yearly','SaaS')");

            t.HasCheckConstraint(
                "chk_subscription_plans_price_amount",
                "price_amount >= 0");

            t.HasCheckConstraint(
                "chk_subscription_plans_commission_rate",
                "commission_rate >= 0 AND commission_rate <= 1");
        });

        // ── Primary Key ────────────────────────────────────────────────────────
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        // ── Columns ───────────────────────────────────────────────────────────
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Tier)
            .HasColumnName("tier")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.BillingCycle)
            .HasColumnName("billing_cycle")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.PriceAmount)
            .HasColumnName("price_amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired()
            .HasDefaultValue("USD");

        builder.Property(x => x.CommissionRate)
            .HasColumnName("commission_rate")
            .HasColumnType("numeric(5,4)")
            .IsRequired();

        builder.Property(x => x.MaxActiveProducts)
            .HasColumnName("max_active_products");

        builder.Property(x => x.MaxMonthlyTryOns)
            .HasColumnName("max_monthly_try_ons");

        builder.Property(x => x.SupportLevel)
            .HasColumnName("support_level")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.IsWhiteLabel)
            .HasColumnName("is_white_label")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IncludesSourceCode)
            .HasColumnName("includes_source_code")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IncludesMobileApps)
            .HasColumnName("includes_mobile_apps")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.HasSla)
            .HasColumnName("has_sla")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.HasDedicatedTeam)
            .HasColumnName("has_dedicated_team")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // ── Ignore BaseEntity fields absent from B.2 schema ───────────────────
        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        // ── Ignore computed domain property (no DB column) ────────────────────
        builder.Ignore(x => x.IsUnlimited);

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(x => x.IsActive)
            .HasDatabaseName("ix_subscription_plans_is_active");

        builder.HasIndex(x => new { x.Tier, x.BillingCycle })
            .HasDatabaseName("ix_subscription_plans_tier_billing_cycle");
    }
}