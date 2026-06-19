using Domain.Entities.Customer;

namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Manages the AI generation deduplication cache.
/// Prevents duplicate paid fal.ai calls for identical inputs.
/// </summary>
public interface IAiGenerationCacheService
{
    // ── Configuration exposed to Application layer ────────────────────────────
    string PipelineVersion { get; }
    string FalAiBodyModelId { get; }
    string FalAiObjectsModelId { get; }
    string FalAiAlignModelId { get; }
    string TryOn2DModelId { get; }
    int FailedRetryWindowHours { get; }

    /// <summary>
    /// Computes a SHA-256 request hash for an avatar generation request.
    /// Hash covers: type + front/side image hashes + height + provider + model IDs + pipeline version.
    /// </summary>
    string ComputeAvatarHash(
        string frontImageHash,
        string sideImageHash,
        decimal heightCm,
        string provider,
        string bodyModelId,
        string measurementModelId,
        string pipelineVersion);

    /// <summary>
    /// Computes a SHA-256 request hash for a 3D try-on request.
    /// </summary>
    string ComputeTryOn3DHash(
        Guid avatarId,
        string avatar3dModelUrl,
        double avatarFocalLength,
        string sourceImageUrl,
        Guid productId,
        string productImageUrl,
        string? selectedSize,
        string? selectedColor,
        string provider,
        string objectsModelId,
        string alignModelId,
        string pipelineVersion);

    /// <summary>
    /// Computes a SHA-256 request hash for a 2D try-on request.
    /// </summary>
    string ComputeTryOn2DHash(
        Guid avatarId,
        string avatarFrontImageUrl,
        Guid productId,
        string productImageUrl,
        string? selectedSize,
        string? selectedColor,
        string provider,
        string tryOn2DModelId,
        string pipelineVersion);

    /// <summary>Computes SHA-256 of raw bytes and returns lowercase hex string.</summary>
    string HashBytes(byte[] bytes);

    /// <summary>
    /// Looks up the cache by request hash.
    /// Returns null if no entry exists.
    /// </summary>
    Task<AiGenerationCache?> GetByHashAsync(string requestHash, CancellationToken ct);

    /// <summary>
    /// Creates a new Processing entry. Returns null if a duplicate race condition is detected
    /// (another request already created the same hash). Caller should reload via GetByHashAsync.
    /// </summary>
    Task<AiGenerationCache?> TryCreateProcessingAsync(
        Guid customerId,
        string requestHash,
        string type,
        string provider,
        string modelId,
        string pipelineVersion,
        string inputJson,
        CancellationToken ct);

    /// <summary>Marks the entry as Completed and persists the result.</summary>
    Task MarkCompletedAsync(
        Guid id,
        string? resultImageUrl,
        string? resultModelUrl,
        string? resultJson,
        CancellationToken ct);

    /// <summary>Marks the entry as Failed and persists error information.</summary>
    Task MarkFailedAsync(Guid id, string errorCode, string errorMessage, CancellationToken ct);

    /// <summary>
    /// Returns true if the customer has exhausted their daily paid avatar generation quota.
    /// Cached hits do not count toward this limit.
    /// </summary>
    Task<bool> IsAvatarQuotaExceededAsync(Guid customerId, CancellationToken ct);

    /// <summary>
    /// Returns true if the customer has exhausted their daily paid try-on generation quota.
    /// Cached hits do not count toward this limit.
    /// </summary>
    Task<bool> IsTryOnQuotaExceededAsync(Guid customerId, CancellationToken ct);
}
