using Domain.Entities.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Subscription;

public sealed class SaasEnquiryConfiguration : IEntityTypeConfiguration<SaasEnquiry>
{
    public void Configure(EntityTypeBuilder<SaasEnquiry> builder)
    {
        // FIX-002: CHECK constraint inside ToTable
        builder.ToTable("saas_enquiries", t =>
        {
            t.HasCheckConstraint(
                "chk_saas_enquiries_status",
                "status IN ('Pending','InProgress','Closed')");
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


        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()             // ← Must come before any default value
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValueSql("'Pending'");    // ← Raw SQL string, not CLR enum value

        builder.Property(x => x.Notes)
            .HasColumnName("notes");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // ── Ignore BaseEntity fields absent from B.20 schema ──────────────────
        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.CreatedBy);
        builder.Ignore(x => x.UpdatedBy);

        // ── Foreign Key ───────────────────────────────────────────────────────
        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(x => x.RetailerId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_saas_enquiries_retailer_id");

        // ── Indexes ───────────────────────────────────────────────────────────
        builder.HasIndex(x => x.RetailerId)
            .HasDatabaseName("ix_saas_enquiries_retailer_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_saas_enquiries_status");
    }
}