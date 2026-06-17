namespace Application.Features.Customer.Avatar.DTOs;

public sealed record AvatarDto(
    Guid Id,
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
    string? BodyShape,
    string? Avatar3dModelUrl,
    /// <summary>
    /// The front-facing person image URL stored during avatar creation.
    /// Non-null when the customer is eligible for 2D try-on (Overlay2D).
    /// </summary>
    string? SourceImageUrl,
    /// <summary>
    /// True when the customer has a source image, meaning 2D try-on is available.
    /// Derived from <see cref="SourceImageUrl"/> for frontend convenience.
    /// </summary>
    bool Has2DCapability,
    /// <summary>
    /// True when the customer has a 3D avatar model, meaning Model3D try-on is available.
    /// Derived from <see cref="Avatar3dModelUrl"/> for frontend convenience.
    /// </summary>
    bool Has3DCapability,
    DateTime LastMeasuredAt);
