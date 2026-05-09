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
    public DateTime LastMeasuredAt { get; private set; }



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
        string? avatar3dModelUrl = null)
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
            LastMeasuredAt = DateTime.UtcNow
        };

        return avatar;
    }

    public void UpdateMeasurements(BodyMeasurements measurements)
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

    public void SetAvatar3dModelUrl(string url)
    {
        Avatar3dModelUrl = url;
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
