using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

public sealed class VirtualTryOnSessionConfiguration : IEntityTypeConfiguration<VirtualTryOnSession>
{
    public void Configure(EntityTypeBuilder<VirtualTryOnSession> builder)
    {
        builder.ToTable("virtual_try_on_sessions", t =>
        {
            t.HasCheckConstraint("ck_virtual_try_on_sessions_type", 
                "session_type IN ('Overlay2D', 'Model3D', 'ARLiveView')");
        });

        builder.HasKey(e => e.Id);

        builder.Property(x => x.CreatedAt)
           .HasDefaultValueSql("now()");

        builder.Property(e => e.SessionType)
            .HasColumnName("session_type")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();
            
        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();
            
        builder.Property(e => e.RecommendedSize)
            .HasColumnName("recommended_size")
            .HasMaxLength(20);
            
        builder.Property(e => e.ConfidenceScore)
            .HasColumnName("confidence_score")
            .HasColumnType("numeric(5,4)");
            
        builder.Property(e => e.ResultImageUrl)
            .HasColumnName("result_image_url")
            .HasColumnType("text");
            
        builder.Property(e => e.DurationSeconds)
            .HasColumnName("duration_seconds");

        builder.HasOne<CustomerAccount>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .HasConstraintName("fk_tryon_customer_accounts");

        builder.HasOne<Avatar>()
            .WithMany()
            .HasForeignKey(x => x.AvatarId)
            .HasConstraintName("fk_tryon_avatars");
        builder.HasOne<Domain.Entities.Retailer.Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .HasConstraintName("fk_tryon_products");

        builder.HasOne<RetailerAccount>()
            .WithMany()
            .HasForeignKey(x => x.RetailerId)
            .HasConstraintName("fk_tryon_retailer_accounts");

        // BaseEntity fields ignored because this table is append-only
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.CreatedBy);
        builder.Ignore(e => e.UpdatedBy);
        builder.Ignore(e => e.IsDeleted);

        // Required indexes for customer history & retailer analytics
        builder.HasIndex(e => new { e.CustomerId, e.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_tryon_sessions_customer_created");

        builder.HasIndex(e => new { e.RetailerId, e.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("idx_tryon_sessions_retailer_created");

        builder.HasIndex(e => e.ProductId)
            .HasDatabaseName("idx_tryon_sessions_product_id");

        builder.HasIndex(e => e.AvatarId)
            .HasDatabaseName("idx_tryon_sessions_avatar_id")
            .HasFilter("avatar_id IS NOT NULL");
    }
}
