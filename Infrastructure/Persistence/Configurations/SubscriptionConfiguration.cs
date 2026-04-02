using Domain.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        // ── FIX-002: Move CHECK constraints inside ToTable ─────────────────────
        builder.ToTable("subscriptions", t =>
        {
            t.HasCheckConstraint(
                "chk_subscriptions_status",
                "status IN ('None','Trial','Active','PendingDowngrade','Expired','Cancelled')");
        });

        // ── Primary Key ────────────────────────────────────────────────────────
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property<uint>("xmin")
            .HasColumnType("xid")
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();

        // ── Columns ───────────────────────────────────────────────────────────
        builder.Property(x => x.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()        // ← Always before any default value config
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValueSql("'None'");  // ← SQL literal, not CLR enum value

        builder.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .IsRequired();

        builder.Property(x => x.EndDate)
            .HasColumnName("end_date");

        builder.Property(x => x.TrialEndsAt)
            .HasColumnName("trial_ends_at");

        // ── Extended Columns (P-015 §12 additions) ────────────────────────────
        builder.Property(x => x.PendingDowngradePlanId)
            .HasColumnName("pending_downgrade_plan_id");

        builder.Property(x => x.PendingDowngradeEffectiveAt)
            .HasColumnName("pending_downgrade_eff_at");

        builder.Property(x => x.IsRecurringEnabled)
            .HasColumnName("is_recurring_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        // ── Audit Columns ─────────────────────────────────────────────────────
        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // ── Ignore BaseEntity fields absent from B.3 schema ───────────────────
        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        // ── Ignore computed domain properties (pure logic, no DB columns) ─────
        builder.Ignore(x => x.IsActive);
        builder.Ignore(x => x.IsInTrial);

        // ── Foreign Keys ──────────────────────────────────────────────────────

        builder.HasOne<Domain.Entities.Retailer.RetailerAccount>()
            .WithOne()
            .HasForeignKey<Subscription>(x => x.RetailerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_subscriptions_retailer_id");

        builder.HasOne(x => x.Plan)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_subscriptions_plan_id");

        builder.HasOne(x => x.PendingDowngradePlan)
            .WithMany()
            .HasForeignKey(x => x.PendingDowngradePlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false)
            .HasConstraintName("fk_subscriptions_pending_downgrade_plan_id");

        // ── Indexes ───────────────────────────────────────────────────────────

        // UNIQUE per retailer — BUG-001 Layer 1: prevents concurrent INSERT of two rows.
        builder.HasIndex(x => x.RetailerId)
            .IsUnique()
            .HasDatabaseName("ix_subscriptions_retailer_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_subscriptions_status");

        builder.HasIndex(x => x.EndDate)
            .HasDatabaseName("ix_subscriptions_end_date");

        builder.HasIndex(x => x.PendingDowngradePlanId)
            .HasDatabaseName("ix_subscriptions_pending_downgrade_plan_id");

        builder.HasIndex(x => x.PlanId)
            .HasDatabaseName("ix_subscriptions_plan_id");
    }
}