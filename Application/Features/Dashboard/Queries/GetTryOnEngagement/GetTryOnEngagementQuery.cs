using Application.Features.Dashboard.DTOs;
using Domain.Enums.Analytics;


namespace Application.Features.Dashboard.Queries.GetTryOnEngagement;

public sealed record GetTryOnEngagementQuery(
    DateOnly From,
    DateOnly To,
    ChartGroupBy GroupBy = ChartGroupBy.Day
) : IRequest<List<TryOnEngagementDto>>;