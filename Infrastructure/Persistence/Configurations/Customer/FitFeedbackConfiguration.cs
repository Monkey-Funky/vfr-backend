using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

public sealed class FitFeedbackConfiguration : IEntityTypeConfiguration<FitFeedback>
{
    public void Configure(EntityTypeBuilder<FitFeedback> builder)
    {
        builder.ToTable("fit_feedback", t =>
        {
            t.HasCheckConstraint("ck_fit_feedback_rating", "fit_rating BETWEEN 1 AND 5");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.PredictedSize)
            .HasColumnName("predicted_size")
            .HasMaxLength(20);

        builder.Property(e => e.ActualSizeNeeded)
            .HasColumnName("actual_size_needed")
            .HasMaxLength(20);

        builder.Property(e => e.FitRating)
            .HasColumnName("fit_rating")
            .IsRequired();

        builder.Property(e => e.FeedbackNotes)
            .HasColumnName("feedback_notes")
            .HasColumnType("text");

        // BaseEntity fields ignored because this table is immutable
        builder.Ignore(e => e.UpdatedAt);
        builder.Ignore(e => e.CreatedBy);
        builder.Ignore(e => e.UpdatedBy);
        builder.Ignore(e => e.IsDeleted);

        // Required indexes for analytics and lookups
        builder.HasIndex(e => e.ProductId)
            .HasDatabaseName("idx_fit_feedback_product_id");

        builder.HasIndex(e => e.CustomerId)
            .HasDatabaseName("idx_fit_feedback_customer_id");

        // Unique constraint: one feedback per order item
        builder.HasIndex(e => e.OrderItemId)
            .IsUnique()
            .HasDatabaseName("uq_fit_feedback_order_item");
    }
}
