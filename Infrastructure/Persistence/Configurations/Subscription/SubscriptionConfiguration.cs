using Domain.Enums.Subscription;

namespace Infrastructure.Persistence.Configurations.Subscription;

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Domain.Entities.Subscriptions.Subscription>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Subscriptions.Subscription> builder)
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

        // ── W-3 FIX ────────────────────────────────────────────────────────────
        // SubscriptionStatus.None has numeric value 0 — the CLR default for enums.
        // Without a sentinel, EF Core omits the column on INSERT and the DB default
        // ('None') always wins, even when the application explicitly sets a different
        // status (e.g. Trial or Active) before the first save.
        //
        // Setting Metadata.Sentinel = SubscriptionStatus.None instructs EF Core:
        //   "Treat None as the unset/default marker; include the column in INSERT for
        //    all other values so the application-assigned status is persisted."
        //
        // This is the direct Metadata-API equivalent of HasSentinelValue() and is
        // fully supported in EF Core 8+. It avoids the generic type-inference issue
        // that prevents the HasSentinelValue extension method from resolving on
        // PropertyBuilder<SubscriptionStatus> when EF Core package versions are mixed.
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()            // ← Always before any default value config
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValueSql("'None'");      // ← SQL literal, not CLR enum value

        // W-3 fix: set sentinel via Metadata API (equivalent to HasSentinelValue).
        builder.Property(x => x.Status).Metadata.Sentinel = SubscriptionStatus.None;

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

        builder.HasOne<RetailerAccount>()
            .WithOne()
            .HasForeignKey<Domain.Entities.Subscriptions.Subscription>(x => x.RetailerId)
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