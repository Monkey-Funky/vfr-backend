using Application.Features.Customer.Avatar.DTOs;

namespace Application.Features.Customer.Avatar.Queries.GetAvatarHistory;

public sealed record GetAvatarMeasurementHistoryQuery(
    Guid CustomerId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<AvatarMeasurementHistoryDto>>;
