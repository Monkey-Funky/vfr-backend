using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;

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

        // ── W-3 FIX ────────────────────────────────────────────────────────────
        // Without a sentinel, EF Core cannot distinguish "application explicitly
        // set Status = Pending" from "property was never assigned" (CLR default = 0).
        // EF therefore omits the column on INSERT and lets the DB default apply —
        // even when a different status was explicitly assigned in code.
        //
        // Setting Metadata.Sentinel = SaasEnquiryStatus.Pending instructs EF Core:
        //   "Treat Pending as the unset/default marker; include the column in INSERT
        //    for every other value so the application-assigned status is persisted."
        //
        // This is the direct Metadata-API equivalent of HasSentinelValue() and is
        // fully supported in EF Core 8+. It avoids the generic type-inference issue
        // that prevents the HasSentinelValue extension method from resolving on
        // PropertyBuilder<SaasEnquiryStatus> when EF Core package versions are mixed.
        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()            // ← Must come before any default value config
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValueSql("'Pending'");   // ← Raw SQL string, not CLR enum value

        // W-3 fix: set sentinel via Metadata API (equivalent to HasSentinelValue).
        builder.Property(x => x.Status).Metadata.Sentinel = SaasEnquiryStatus.Pending;

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