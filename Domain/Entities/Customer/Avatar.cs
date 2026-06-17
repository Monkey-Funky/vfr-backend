using Domain.Exceptions;

namespace Domain.Entities.Customer;

public sealed class Avatar : BaseEntity
{
    public Guid CustomerId { get; private set; }

    public decimal HeightCm { get; private set; }
    public decimal WeightKg { get; private set; }

    public decimal? ChestCm { get; private set; }
    public decimal? WaistCm { get; private set; }
    public decimal? HipsCm { get; private set; }
    public decimal? ShoulderWidthCm { get; private set; }
    public decimal? InseamCm { get; private set; }
    public decimal? NeckCm { get; private set; }
    public decimal? ArmLengthCm { get; private set; }
    public decimal? ShoeSizeEu { get; private set; }

    public string? BodyShape { get; private set; }
    public string? Avatar3dModelUrl { get; private set; }
    public double? AvatarFocalLength { get; private set; }
    /// <summary>Original person image URL used to generate the body mesh. Required by SAM 3D Align.</summary>
    public string? SourceImageUrl { get; private set; }
    public DateTime LastMeasuredAt { get; private set; }

    private readonly List<AvatarMeasurementHistory> _measurementHistories = [];
    public IReadOnlyCollection<AvatarMeasurementHistory> MeasurementHistories => _measurementHistories.AsReadOnly();

    private Avatar() { }

    public static Avatar Create(
        Guid customerId,
        decimal heightCm,
        decimal weightKg,
        decimal? chestCm = null,
        decimal? waistCm = null,
        decimal? hipsCm = null,
        decimal? shoulderWidthCm = null,
        decimal? inseamCm = null,
        decimal? neckCm = null,
        decimal? armLengthCm = null,
        decimal? shoeSizeEu = null,
        string? bodyShape = null,
        string? avatar3dModelUrl = null,
        double? avatarFocalLength = null,
        string? sourceImageUrl = null)
    {
        if (heightCm <= 0) throw new BusinessRuleException("INVALID_HEIGHT", "Height must be greater than zero.");
        if (weightKg <= 0) throw new BusinessRuleException("INVALID_WEIGHT", "Weight must be greater than zero.");

        var avatar = new Avatar
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            HeightCm = heightCm,
            WeightKg = weightKg,
            ChestCm = chestCm,
            WaistCm = waistCm,
            HipsCm = hipsCm,
            ShoulderWidthCm = shoulderWidthCm,
            InseamCm = inseamCm,
            NeckCm = neckCm,
            ArmLengthCm = armLengthCm,
            ShoeSizeEu = shoeSizeEu,
            BodyShape = bodyShape,
            Avatar3dModelUrl = avatar3dModelUrl,
            AvatarFocalLength = avatarFocalLength,
            SourceImageUrl = sourceImageUrl,
            LastMeasuredAt = DateTime.UtcNow
        };

        return avatar;
    }

    /// <summary>
    /// Updates all body measurements and records an immutable history snapshot.
    /// </summary>
    /// <param name="measurements">The new measurement values.</param>
    /// <param name="source">
    /// The measurement source: "Manual", "BodyScan", or "AIEstimate".
    /// Passed through to <see cref="AvatarMeasurementHistory"/> so the correct
    /// source is recorded rather than always defaulting to "Manual".
    /// </param>
    public void UpdateMeasurements(BodyMeasurements measurements, string source = "Manual")
    {
        ArgumentNullException.ThrowIfNull(measurements);

        if (measurements.HeightCm <= 0) throw new BusinessRuleException("INVALID_HEIGHT", "Height must be greater than zero.");
        if (measurements.WeightKg <= 0) throw new BusinessRuleException("INVALID_WEIGHT", "Weight must be greater than zero.");

        HeightCm = measurements.HeightCm;
        WeightKg = measurements.WeightKg;
        ChestCm = measurements.ChestCm;
        WaistCm = measurements.WaistCm;
        HipsCm = measurements.HipsCm;
        ShoulderWidthCm = measurements.ShoulderWidthCm;
        InseamCm = measurements.InseamCm;
        NeckCm = measurements.NeckCm;
        ArmLengthCm = measurements.ArmLengthCm;
        ShoeSizeEu = measurements.ShoeSizeEu;
        BodyShape = measurements.BodyShape;

        LastMeasuredAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Serializes body measurements to a JSON string for history snapshots.
    /// Public so that command handlers can build history snapshots explicitly.
    /// </summary>
    public static string BuildMeasurementJson(BodyMeasurements m)
    {
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        return string.Concat(
            "{",
            $"\"HeightCm\":{m.HeightCm.ToString(ic)},",
            $"\"WeightKg\":{m.WeightKg.ToString(ic)},",
            $"\"ChestCm\":{(m.ChestCm.HasValue ? m.ChestCm.Value.ToString(ic) : "null")},",
            $"\"WaistCm\":{(m.WaistCm.HasValue ? m.WaistCm.Value.ToString(ic) : "null")},",
            $"\"HipsCm\":{(m.HipsCm.HasValue ? m.HipsCm.Value.ToString(ic) : "null")},",
            $"\"ShoulderWidthCm\":{(m.ShoulderWidthCm.HasValue ? m.ShoulderWidthCm.Value.ToString(ic) : "null")},",
            $"\"InseamCm\":{(m.InseamCm.HasValue ? m.InseamCm.Value.ToString(ic) : "null")},",
            $"\"NeckCm\":{(m.NeckCm.HasValue ? m.NeckCm.Value.ToString(ic) : "null")},",
            $"\"ArmLengthCm\":{(m.ArmLengthCm.HasValue ? m.ArmLengthCm.Value.ToString(ic) : "null")},",
            $"\"ShoeSizeEu\":{(m.ShoeSizeEu.HasValue ? m.ShoeSizeEu.Value.ToString(ic) : "null")},",
            $"\"BodyShape\":{(m.BodyShape is not null ? $"\"{m.BodyShape}\"" : "null")}",
            "}");
    }

    public void SetAvatar3dModelUrl(string url, double? focalLength = null, string? sourceImageUrl = null)
    {
        Avatar3dModelUrl = url;
        AvatarFocalLength = focalLength;
        if (sourceImageUrl is not null)
            SourceImageUrl = sourceImageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the person/source image used for try-on (2D Overlay garment compositing and,
    /// when available, as the reference image for SAM 3D Align). Intentionally independent
    /// of <see cref="SetAvatar3dModelUrl"/> so the source image can be persisted even when
    /// 3D body-model generation fails or is disabled — keeping 2D try-on available.
    /// </summary>
    public void SetSourceImageUrl(string sourceImageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImageUrl);
        SourceImageUrl = sourceImageUrl;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed record BodyMeasurements(
    decimal HeightCm,
    decimal WeightKg,
    decimal? ChestCm,
    decimal? WaistCm,
    decimal? HipsCm,
    decimal? ShoulderWidthCm,
    decimal? InseamCm,
    decimal? NeckCm,
    decimal? ArmLengthCm,
    decimal? ShoeSizeEu,
    string? BodyShape
);