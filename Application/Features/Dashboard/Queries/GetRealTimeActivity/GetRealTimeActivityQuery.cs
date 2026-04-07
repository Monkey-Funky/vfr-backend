using Application.Features.Dashboard.DTOs;


namespace Application.Features.Dashboard.Queries.GetRealTimeActivity;

public sealed record GetRealTimeActivityQuery : IRequest<List<ActivityEventDto>>;
