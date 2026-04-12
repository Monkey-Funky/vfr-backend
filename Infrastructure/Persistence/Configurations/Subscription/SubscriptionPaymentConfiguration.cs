using Domain.Entities.Subscriptions;

namespace Infrastructure.Persistence.Configurations.Subscription;

public sealed class SubscriptionPaymentConfiguration
    : IEntityTypeConfiguration<SubscriptionPayment>
{
    public void Configure(EntityTypeBuilder<SubscriptionPayment> builder)
    {
        // FIX-002: CHECK constraints inside ToTable
        builder.ToTable("subscription_payments", t =>
        {
            t.HasCheckConstraint(
                "chk_subscription_payments_status",
                "status IN ('Pending','Processing','Completed','Failed','Refunded')");

            t.HasCheckConstraint(
                "chk_subscription_payments_amount",
                "amount >= 0");
        });

        // ── Primary Key ────────────────────────────────────────────────────────
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        // ── Columns ───────────────────────────────────────────────────────────
        builder.Property(x => x.RetailerId)
            .HasColumnName("retailer_id")
            .IsRequired();

        builder.Property(x => x.SubscriptionPlanId)
            .HasColumnName("subscription_plan_id")
            .IsRequired();

        builder.Property(x => x.PaymentMethodId)
            .HasColumnName("payment_method_id");

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        // FIX-003: HasConversion<string>() first, then HasDefaultValueSql (raw SQL literal).
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()             // ← Always before default value
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValueSql("'Pending'");    // ← SQL literal, not CLR enum value

        builder.Property(x => x.IsRecurring)
            .HasColumnName("is_recurring")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.PeriodStartDate)
            .HasColumnName("period_start_date")
            .IsRequired();

        builder.Property(x => x.PeriodEndDate)
            .HasColumnName("period_end_date")
            .IsRequired();

        builder.Property(x => x.StripePaymentIntentId)
            .HasColumnName("stripe_payment_intent_id")
            .HasMaxLength(100);

        builder.Property(x => x.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // ── Ignore immutable fields not in B.14 schema (BUG-010) ─────────────
        builder.Ignore(x => x.UpdatedAt);
        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        // ── Foreign Keys ──────────────────────────────────────────────────────
        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(x => x.RetailerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_subscription_payments_retailer_id");

        builder.HasOne(x => x.Plan)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_subscription_payments_subscription_plan_id");

        // PaymentMethodId is nullable — payment method may be soft-deleted after payment
        builder.HasOne<PaymentMethod>()
            .WithMany()
            .HasForeignKey(x => x.PaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false)
            .HasConstraintName("fk_subscription_payments_payment_method_id");

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(x => x.RetailerId)
            .HasDatabaseName("ix_subscription_payments_retailer_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_subscription_payments_status");

        builder.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("ix_subscription_payments_created_at");

        builder.HasIndex(x => x.PaymentMethodId)
            .HasDatabaseName("ix_subscription_payments_payment_method_id");

        builder.HasIndex(x => x.SubscriptionPlanId)
            .HasDatabaseName("ix_subscription_payments_subscription_plan_id");

        // BUG-002 reconciliation index: detects orphaned Completed payments.
        builder.HasIndex(x => new { x.Status, x.StripePaymentIntentId })
            .HasDatabaseName("ix_subscription_payments_status_stripe_intent")
            .HasFilter("status = 'Completed' AND stripe_payment_intent_id IS NOT NULL");
    }
}