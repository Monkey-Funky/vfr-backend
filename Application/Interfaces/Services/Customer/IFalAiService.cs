namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Abstraction over the fal.ai 3D API suite.
/// Used for generating 3D avatar models, 3D clothing objects, and aligned scenes.
/// </summary>
public interface IFalAiService
{
    /// <summary>
    /// Calls the Hyper3D Rodin API to generate a production-ready, textured 3D avatar
    /// from a person's full-body photo. The output is a GLB file with PBR materials,
    /// realistic clothing/skin/hair textures, and clean topology.
    /// </summary>
    /// <param name="imageUrl">Public URL of the person's full-body photo.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The GLB URL of the generated avatar.</returns>
    Task<FalAvatarResult> GenerateAvatar3dAsync(string imageUrl, CancellationToken ct = default);

    /// <summary>
    /// Calls fal.ai SAM 3D Objects API to generate a GLB 3D model of a clothing item.
    /// </summary>
    /// <param name="imageUrl">Public URL of the product image.</param>
    /// <param name="prompt">Description of the clothing item (e.g. "blue cotton t-shirt").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The GLB URL of the generated clothing object.</returns>
    Task<FalObjectResult> GenerateObject3dAsync(string imageUrl, string prompt, CancellationToken ct = default);

    /// <summary>
    /// Calls fal.ai SAM 3D Align API to combine a body mesh and clothing mesh into one scene.
    /// </summary>
    /// <param name="imageUrl">Public URL of the reference image (typically the product image).</param>
    /// <param name="bodyMeshUrl">GLB URL of the body mesh.</param>
    /// <param name="objectMeshUrl">GLB URL of the clothing object mesh.</param>
    /// <param name="focalLength">Camera focal length from the body generation step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The GLB URL of the combined scene.</returns>
    Task<string> AlignSceneAsync(string imageUrl, string bodyMeshUrl, string objectMeshUrl, double focalLength, CancellationToken ct = default);
}

/// <summary>Result from the Hyper3D Rodin avatar generation API.</summary>
public sealed record FalAvatarResult(string GlbUrl);

/// <summary>Result from the SAM 3D Objects generation API.</summary>
public sealed record FalObjectResult(string GlbUrl);
