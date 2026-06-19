using Domain.Entities.Customer;

namespace Infrastructure.Persistence.Configurations.Customer;

public sealed class AiGenerationCacheConfiguration : IEntityTypeConfiguration<AiGenerationCache>
{
    public void Configure(EntityTypeBuilder<AiGenerationCache> builder)
    {
        builder.ToTable("ai_generation_cache");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("uuid");

        builder.Property(e => e.CustomerId)
            .HasColumnName("customer_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.RequestHash)
            .HasColumnName("request_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasColumnName("type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.PipelineVersion)
            .HasColumnName("pipeline_version")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.InputJson)
            .HasColumnName("input_json")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(e => e.ResultJson)
            .HasColumnName("result_json")
            .HasColumnType("text");

        builder.Property(e => e.ResultImageUrl)
            .HasColumnName("result_image_url")
            .HasColumnType("text");

        builder.Property(e => e.ResultModelUrl)
            .HasColumnName("result_model_url")
            .HasColumnType("text");

        builder.Property(e => e.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(100);

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("timestamptz");

        builder.Property(e => e.FailedAt)
            .HasColumnName("failed_at")
            .HasColumnType("timestamptz");

        // Unique constraint — prevents duplicate concurrent requests for the same hash.
        builder.HasIndex(e => e.RequestHash)
            .IsUnique()
            .HasDatabaseName("uq_ai_generation_cache_request_hash");

        builder.HasIndex(e => e.Status)
            .HasDatabaseName("idx_ai_generation_cache_status");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("idx_ai_generation_cache_created_at");

        // For per-customer daily quota queries
        builder.HasIndex(e => new { e.CustomerId, e.Type, e.CreatedAt })
            .HasDatabaseName("idx_ai_generation_cache_customer_type_created");
    }
}
