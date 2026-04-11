using Application.Features.Dashboard.DTOs;
using Domain.Enums.Analytics;
namespace Application.Features.Dashboard.Queries.GetRevenueChart;
/// <summary>
/// GroupBy accepts only: "Day" | "Week" | "Month".
/// DateFrom must not be more than 2 years in the past.
/// DateTo must not be in the future.
/// </summary>
public sealed record GetRevenueChartQuery(
    DateOnly From,
    DateOnly To,
    ChartGroupBy GroupBy = ChartGroupBy.Day
) : IRequest<List<ChartDataPoint>>;