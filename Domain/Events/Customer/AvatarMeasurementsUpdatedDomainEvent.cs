namespace Domain.Events.Customer;

/// <summary>
/// Raised when avatar measurements are updated.
/// The handler creates an AvatarMeasurementHistory record.
/// </summary>
public sealed record AvatarMeasurementsUpdatedDomainEvent(
    Guid AvatarId,
    string MeasurementDataJson,
    string Source) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
