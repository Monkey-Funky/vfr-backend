using System.Text.Json.Serialization;

namespace Domain.Enums.Customer;

/// <summary>
/// The mode of a virtual try-on session.
/// Serialized as its string name (e.g. "Model3D") for a stable frontend contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TryOnSessionType
{
    Overlay2D,
    Model3D,
    ARLiveView
}
