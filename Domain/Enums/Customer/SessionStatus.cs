using System.Text.Json.Serialization;

namespace Domain.Enums.Customer;

/// <summary>
/// The lifecycle status of a virtual try-on session.
/// Serialized as its string name (e.g. "Completed") for a stable frontend contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionStatus
{
    Processing,
    Completed,
    Failed
}
