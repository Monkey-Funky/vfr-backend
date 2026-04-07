using Application.Features.Dashboard.DTOs;
using Domain.Enums.Analytics;


namespace Application.Features.Dashboard.Queries.GetSessionsChart;

public sealed record GetSessionsChartQuery(
    DateOnly From,
    DateOnly To,
    ChartGroupBy GroupBy = ChartGroupBy.Day
) : IRequest<List<ChartDataPoint>>;