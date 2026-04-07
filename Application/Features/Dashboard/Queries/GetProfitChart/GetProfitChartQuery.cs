using Application.Features.Dashboard.DTOs;
using Domain.Enums.Analytics;

namespace Application.Features.Dashboard.Queries.GetProfitChart;

public sealed record GetProfitChartQuery(
    DateOnly From,
    DateOnly To,
    ChartGroupBy GroupBy = ChartGroupBy.Day
) : IRequest<List<ChartDataPoint>>;