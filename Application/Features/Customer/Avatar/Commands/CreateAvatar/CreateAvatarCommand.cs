namespace Application.Features.Customer.Avatar.Commands.CreateAvatar;

public sealed record CreateAvatarCommand(
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
    string Source) : IRequest<Guid>;
