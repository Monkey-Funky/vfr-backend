

namespace Domain.Entities.Analytics;

/// <summary>
/// Records fit accuracy feedback: whether the VFR-recommended size matched
/// the size the customer actually ordered or preferred.
/// Powers the FitAccuracy analytics chart.
/// NOT soft-deleted — immutable analytics record.
/// </summary>
public sealed class FitAccuracy
{
    public Guid Id { get; private set; }
    public Guid RetailerId { get; private set; }
    public Guid? ProductId { get; private set; }

    /// <summary>Size recommended by the VFR AI engine (e.g. "M", "L", "38").</summary>
    public string PredictedSize { get; private set; } = string.Empty;

    /// <summary>Size the customer actually chose or confirmed as correct.</summary>
    public string ActualSize { get; private set; } = string.Empty;

    /// <summary>True when PredictedSize == ActualSize; false otherwise.</summary>
    public bool WasAccurate { get; private set; }

    /// <summary>FK to the TryOnSession that generated this prediction.</summary>
    public Guid? SessionId { get; private set; }

    public DateTime RecordedAt { get; private set; }

    private FitAccuracy() { } // EF Core

    public FitAccuracy(
        Guid retailerId,
        Guid? productId,
        string predictedSize,
        string actualSize,
        Guid? sessionId)
    {
        Id = Guid.NewGuid();
        RetailerId = retailerId;
        ProductId = productId;
        PredictedSize = predictedSize;
        ActualSize = actualSize;
        WasAccurate = string.Equals(predictedSize, actualSize, StringComparison.OrdinalIgnoreCase);
        SessionId = sessionId;
        RecordedAt = DateTime.UtcNow;
    }
}