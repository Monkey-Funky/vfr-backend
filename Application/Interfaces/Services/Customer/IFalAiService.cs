namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Abstraction over the fal.ai API suite.
/// Provides image preprocessing (background removal, upscaling) and
/// 3D generation (avatar, clothing objects, scene alignment).
/// </summary>
public interface IFalAiService
{
    // ── Image Preprocessing ──────────────────────────────────────────────

    /// <summary>
    /// Removes the background from a human subject photo using BiRefNet v2 (Portrait model).
    /// Returns a clean, transparent PNG with only the person isolated.
    /// </summary>
    /// <param name="imageUrl">Public URL of the image to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>URL of the background-removed image.</returns>
    Task<string> RemoveBackgroundAsync(string imageUrl, CancellationToken ct = default);

    /// <summary>
    /// Upscales an image using AuraSR (4× GAN-based super-resolution).
    /// Enhances clarity and detail for better 3D reconstruction.
    /// </summary>
    /// <param name="imageUrl">Public URL of the image to upscale.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>URL of the upscaled image.</returns>
    Task<string> UpscaleImageAsync(string imageUrl, CancellationToken ct = default);

    // ── 3D Generation ────────────────────────────────────────────────────

    /// <summary>
    /// Generates a production-ready, textured 3D avatar from one or more person photos
    /// using Hyper3D Rodin. Output is a GLB file with PBR materials.
    /// Multiple images (front + side) are fused via concat mode for better reconstruction.
    /// </summary>
    /// <param name="imageUrls">Public URLs of the person's full-body photos (front, side, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The GLB URL of the generated avatar.</returns>
    Task<FalAvatarResult> GenerateAvatar3dAsync(IReadOnlyList<string> imageUrls, CancellationToken ct = default);

    /// <summary>
    /// Calls fal.ai SAM 3D Objects API to generate a GLB 3D model of a clothing item.
    /// </summary>
    Task<FalObjectResult> GenerateObject3dAsync(string imageUrl, string prompt, CancellationToken ct = default);

    /// <summary>
    /// Calls fal.ai SAM 3D Align API to combine a body mesh and clothing mesh into one scene.
    /// </summary>
    Task<string> AlignSceneAsync(string imageUrl, string bodyMeshUrl, string objectMeshUrl, double focalLength, CancellationToken ct = default);
}

/// <summary>Result from the Hyper3D Rodin avatar generation API.</summary>
public sealed record FalAvatarResult(string GlbUrl);

/// <summary>Result from the SAM 3D Objects generation API.</summary>
public sealed record FalObjectResult(string GlbUrl);
