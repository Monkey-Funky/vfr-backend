namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Abstraction over the fal.ai SAM 3D API suite.
/// Used for generating 3D body meshes, 3D clothing objects, and aligned scenes.
/// </summary>
public interface IFalAiService
{
    /// <summary>
    /// Calls fal.ai SAM 3D Body API to generate a GLB 3D model from a person's photo.
    /// </summary>
    /// <param name="imageUrl">Public URL of the person's full-body photo.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The GLB URL and focal length metadata.</returns>
    Task<FalBodyResult> GenerateBody3dAsync(string imageUrl, CancellationToken ct = default);

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

/// <summary>Result from the SAM 3D Body generation API.</summary>
public sealed record FalBodyResult(string GlbUrl, double FocalLength);

/// <summary>Result from the SAM 3D Objects generation API.</summary>
public sealed record FalObjectResult(string GlbUrl);
