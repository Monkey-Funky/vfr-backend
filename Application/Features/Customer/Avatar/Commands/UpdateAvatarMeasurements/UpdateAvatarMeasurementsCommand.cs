namespace Application.Features.Customer.Avatar.Commands.UpdateAvatarMeasurements;

public sealed record UpdateAvatarMeasurementsCommand(
    Guid AvatarId,
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
    string Source) : IRequest;
