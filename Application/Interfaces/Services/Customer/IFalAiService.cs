namespace Application.Interfaces.Services.Customer;

/// <summary>
/// Abstraction over the fal.ai SAM 3D API suite.
/// Three specialized APIs: Body (human), Objects (clothing), Align (scene).
/// Each costs $0.02 and completes in 5-10 seconds.
/// </summary>
public interface IFalAiService
{
    /// <summary>
    /// SAM 3D Body — reconstructs human body geometry from a single image.
    /// Returns GLB mesh with skeletal keypoints and camera focal length.
    /// Cost: $0.02 | Speed: 5-10s
    /// </summary>
    Task<FalBodyResult> GenerateBody3dAsync(string imageUrl, CancellationToken ct = default);

    /// <summary>
    /// SAM 3D Objects — reconstructs a 3D mesh of a clothing item.
    /// Cost: $0.02 | Speed: 5-10s
    /// </summary>
    Task<FalObjectResult> GenerateObject3dAsync(string imageUrl, string prompt, CancellationToken ct = default);

    /// <summary>
    /// SAM 3D Align — combines body + clothing meshes into one aligned scene.
    /// Cost: $0.02 | Speed: 5-10s
    /// </summary>
    Task<string> AlignSceneAsync(string imageUrl, string bodyMeshUrl, string objectMeshUrl, double focalLength, CancellationToken ct = default);
}

/// <summary>
/// Result from SAM 3D Body — includes GLB URL and focal length
/// (focal length is needed for the Align step later).
/// </summary>
public sealed record FalBodyResult(string GlbUrl, double FocalLength);

/// <summary>Result from SAM 3D Objects.</summary>
public sealed record FalObjectResult(string GlbUrl);
