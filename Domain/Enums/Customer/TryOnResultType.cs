using System.Text.Json.Serialization;

namespace Domain.Enums.Customer;

/// <summary>
/// Describes what kind of artifact a try-on session produced, so the frontend
/// knows how to render <c>ResultImageUrl</c> (a 3D scene/model vs a flat 2D image).
/// Serialized as its string name (e.g. "Image2D") for a stable frontend contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TryOnResultType
{
    /// <summary>A 3D scene/model (GLB) — the existing default try-on output.</summary>
    Model3D,

    /// <summary>A flat 2D try-on image produced by the Overlay2D pipeline.</summary>
    Image2D,

    /// <summary>An AR live-view result.</summary>
    ARLiveView
}
