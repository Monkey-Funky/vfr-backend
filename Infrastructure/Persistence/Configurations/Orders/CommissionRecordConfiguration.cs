using Domain.Entities.Orders;
using Domain.Entities.Subscriptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Persistence.Configurations.Orders;

/// <summary>
/// EF Core configuration for CommissionRecord.
/// Maps to the commission_records table with an immutable financial audit schema.
/// </summary>
public sealed class CommissionRecordConfiguration : IEntityTypeConfiguration<CommissionRecord>
{
    public void Configure(EntityTypeBuilder<CommissionRecord> builder)
    {
        builder.ToTable("commission_records", t =>
        {
            t.HasCheckConstraint(
                "ck_commission_records_rate",
                "commission_rate >= 0 AND commission_rate <= 1");

            t.HasCheckConstraint(
                "ck_commission_records_amounts_positive",
                "order_total >= 0 AND commission_amount >= 0");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()")
            .IsRequired();

        builder.Property(c => c.RetailerId)
            .HasColumnName("retailer_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.OrderId)
            .HasColumnName("order_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.SubscriptionPlanId)
            .HasColumnName("subscription_plan_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(c => c.CommissionRate)
            .HasColumnName("commission_rate")
            .HasPrecision(5, 4)   // e.g. 0.0500 for 5%
            .IsRequired();

        builder.Property(c => c.OrderTotal)
            .HasColumnName("order_total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.CommissionAmount)
            .HasColumnName("commission_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(c => c.Currency)
            .HasColumnName("currency")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(c => c.DeliveredAt)
            .HasColumnName("delivered_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        // BaseEntity.CreatedAt maps to "created_at" normally
        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // commission_records is an immutable audit table — these columns don't exist
        builder.Ignore(c => c.UpdatedAt);
        builder.Ignore(c => c.CreatedBy);
        builder.Ignore(c => c.UpdatedBy);
        builder.Ignore(c => c.IsDeleted);

        // No global query filter — commission records are never soft-deleted

        // FK: commission_records.retailer_id → retailer_accounts.id
        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(c => c.RetailerId)
            .OnDelete(DeleteBehavior.Restrict);   // Restrict: retain financial history if account deleted

        // FK: commission_records.order_id → orders.id
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(c => c.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: commission_records.subscription_plan_id → subscription_plans.id
        builder.HasOne<SubscriptionPlan>()
            .WithMany()
            .HasForeignKey(c => c.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(c => c.RetailerId)
            .HasDatabaseName("idx_commission_records_retailer_id");

        builder.HasIndex(c => c.OrderId)
            .IsUnique()
            .HasDatabaseName("idx_commission_records_order_id");   // one commission per order

        builder.HasIndex(c => new { c.RetailerId, c.DeliveredAt })
            .HasDatabaseName("idx_commission_records_retailer_delivered_at");
    }
}