using Domain.Exceptions;

namespace Domain.Entities.Customer;

public sealed class AvatarMeasurementHistory : BaseEntity
{
    public Guid AvatarId { get; private set; }
    
    /// <summary>
    /// Full snapshot of measurements stored as JSON string (mapped to jsonb in EF Core)
    /// </summary>
    public string MeasurementData { get; private set; } = string.Empty;
    
    /// <summary>
    /// Source of the measurement update (e.g., Manual, BodyScan, AIEstimate)
    /// </summary>
    public string Source { get; private set; } = string.Empty;
    
    public DateTime RecordedAt { get; private set; }

    private AvatarMeasurementHistory() { }

    public static AvatarMeasurementHistory CreateSnapshot(Guid avatarId, string measurementDataJson, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(measurementDataJson, nameof(measurementDataJson));
        ArgumentException.ThrowIfNullOrWhiteSpace(source, nameof(source));

        if (!new[] { "Manual", "BodyScan", "AIEstimate" }.Contains(source))
            throw new BusinessRuleException("INVALID_SOURCE", "Measurement source must be Manual, BodyScan, or AIEstimate.");

        return new AvatarMeasurementHistory
        {
            Id = Guid.NewGuid(),
            AvatarId = avatarId,
            MeasurementData = measurementDataJson,
            Source = source,
            RecordedAt = DateTime.UtcNow
        };
    }
}
