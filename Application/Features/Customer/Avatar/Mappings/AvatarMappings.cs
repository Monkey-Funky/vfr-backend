using Application.Features.Customer.Avatar.DTOs;
using Domain.Entities.Customer;

namespace Application.Features.Customer.Avatar.Mappings;

public static class AvatarMappings
{
    public static AvatarDto ToDto(this Domain.Entities.Customer.Avatar avatar)
    {
        return new AvatarDto(
            Id: avatar.Id,
            HeightCm: avatar.HeightCm,
            WeightKg: avatar.WeightKg,
            ChestCm: avatar.ChestCm,
            WaistCm: avatar.WaistCm,
            HipsCm: avatar.HipsCm,
            ShoulderWidthCm: avatar.ShoulderWidthCm,
            InseamCm: avatar.InseamCm,
            NeckCm: avatar.NeckCm,
            ArmLengthCm: avatar.ArmLengthCm,
            ShoeSizeEu: avatar.ShoeSizeEu,
            BodyShape: avatar.BodyShape,
            Avatar3dModelUrl: avatar.Avatar3dModelUrl,
            LastMeasuredAt: avatar.LastMeasuredAt
        );
    }

    public static AvatarMeasurementHistoryDto ToDto(this AvatarMeasurementHistory history)
    {
        return new AvatarMeasurementHistoryDto(
            Id: history.Id,
            MeasurementDataJson: history.MeasurementData,
            Source: history.Source,
            RecordedAt: history.RecordedAt
        );
    }
}
