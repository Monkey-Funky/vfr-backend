using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

public sealed class AvatarConfiguration : IEntityTypeConfiguration<Avatar>
{
    public void Configure(EntityTypeBuilder<Avatar> builder)
    {
        builder.ToTable("avatars", t =>
        {
            t.HasCheckConstraint("ck_avatars_body_shape",
                "body_shape IN ('Rectangle', 'Triangle', 'InvertedTriangle', 'Hourglass', 'Apple', 'Pear')");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.HeightCm).HasColumnName("height_cm").HasColumnType("numeric(5,1)").IsRequired();
        builder.Property(a => a.WeightKg).HasColumnName("weight_kg").HasColumnType("numeric(5,1)").IsRequired();
        builder.Property(a => a.ChestCm).HasColumnName("chest_cm").HasColumnType("numeric(5,1)");
        builder.Property(a => a.WaistCm).HasColumnName("waist_cm").HasColumnType("numeric(5,1)");
        builder.Property(a => a.HipsCm).HasColumnName("hips_cm").HasColumnType("numeric(5,1)");
        builder.Property(a => a.ShoulderWidthCm).HasColumnName("shoulder_width_cm").HasColumnType("numeric(5,1)");
        builder.Property(a => a.InseamCm).HasColumnName("inseam_cm").HasColumnType("numeric(5,1)");
        builder.Property(a => a.NeckCm).HasColumnName("neck_cm").HasColumnType("numeric(5,1)");
        builder.Property(a => a.ArmLengthCm).HasColumnName("arm_length_cm").HasColumnType("numeric(5,1)");
        builder.Property(a => a.ShoeSizeEu).HasColumnName("shoe_size_eu").HasColumnType("numeric(4,1)");

        builder.Property(a => a.BodyShape).HasColumnName("body_shape").HasMaxLength(30);
        builder.Property(a => a.Avatar3dModelUrl).HasColumnName("avatar_3d_model_url").HasColumnType("text");
        builder.Property(a => a.LastMeasuredAt).HasColumnName("last_measured_at").IsRequired().HasDefaultValueSql("now()");

        // Global Query Filter for Soft Delete
        builder.HasQueryFilter(a => !a.IsDeleted);

        // One-to-one relationship with CustomerAccount
        builder.HasOne<CustomerAccount>()
            .WithOne()
            .HasForeignKey<Avatar>(a => a.CustomerId)
            .HasConstraintName("fk_avatars_customer_accounts_customer_id");

        // Single active avatar per customer
        builder.HasIndex(a => a.CustomerId)
               .IsUnique()
               .HasFilter("is_deleted = false")
               .HasDatabaseName("uq_avatars_customer_id");
    }
}
