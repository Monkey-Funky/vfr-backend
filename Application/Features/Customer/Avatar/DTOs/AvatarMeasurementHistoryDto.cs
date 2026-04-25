namespace Application.Features.Customer.Avatar.DTOs;

public sealed record AvatarMeasurementHistoryDto(
    Guid Id,
    string MeasurementDataJson,
    string Source,
    DateTime RecordedAt);
