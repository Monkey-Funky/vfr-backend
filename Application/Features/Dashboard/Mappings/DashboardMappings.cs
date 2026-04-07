using Application.Features.Dashboard.DTOs;
using Domain.Entities.Analytics;
using Domain.Enums.Analytics;


namespace Application.Features.Dashboard.Mappings;

public static class DashboardMappings
{
    public static ActivityEventDto ToDto(this Domain.Entities.Analytics.ActivityEvent e)
        => new(
            Id: e.Id,
            EventType: e.EventType,
            ResourceId: e.ResourceId,
            EventData: e.EventData,
            CreatedAt: e.CreatedAt
        );

    public static ReportStatusDto ToStatusDto(this Report report)
        => new(
            ReportId: report.Id,
            Status: report.Status.ToString(),
            ReportUrl: report.ReportUrl,
            FailureReason: report.FailureReason,
            CreatedAt: report.CreatedAt,
            CompletedAt: report.CompletedAt
        );

    /// <summary>Formats a DateOnly into a chart label appropriate for the grouping.</summary>
    public static string ToChartLabel(this DateOnly date, ChartGroupBy groupBy)
        => groupBy switch
        {
            ChartGroupBy.Day => date.ToString("yyyy-MM-dd"),
            ChartGroupBy.Week => $"{date.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):D2}",
            ChartGroupBy.Month => date.ToString("yyyy-MM"),
            _ => date.ToString("yyyy-MM-dd")
        };
}