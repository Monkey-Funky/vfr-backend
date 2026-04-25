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
    DateTime LastMeasuredAt);
