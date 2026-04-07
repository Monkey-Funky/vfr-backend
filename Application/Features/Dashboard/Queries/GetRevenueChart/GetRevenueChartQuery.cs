using Application.Features.Dashboard.DTOs;
using Domain.Enums.Analytics;


namespace Application.Features.Dashboard.Queries.GetRevenueChart;


public sealed record GetRevenueChartQuery(
    DateOnly From,
    DateOnly To,
    ChartGroupBy GroupBy = ChartGroupBy.Day
) : IRequest<List<ChartDataPoint>>;