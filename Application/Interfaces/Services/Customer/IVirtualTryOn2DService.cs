namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Abstraction over a 2D image-based virtual try-on provider (e.g. fal.ai FASHN).
/// Takes a person image + a garment image and returns a single composited 2D image.
/// This runs the Overlay2D path and is fully independent of the 3D SAM pipeline.
/// </summary>
public interface IVirtualTryOn2DService
{
    Task<TryOn2DResult> ProcessTryOnAsync(TryOn2DRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Input for a 2D try-on. The caller is responsible for resolving the public
/// <paramref name="PersonImageUrl"/> (customer front image) and
/// <paramref name="GarmentImageUrl"/> (product primary image) before calling.
/// </summary>
public sealed record TryOn2DRequest(
    Guid CustomerId,
    Guid ProductId,
    Guid AvatarId,
    string PersonImageUrl,
    string GarmentImageUrl,
    string? Category,
    string? SelectedSize,
    string? SelectedColor
);

/// <summary>Output of a 2D try-on — a single result image and provider metadata.</summary>
public sealed record TryOn2DResult(
    string ResultImageUrl,
    decimal? ConfidenceScore,
    int? DurationSeconds,
    string Provider
);
