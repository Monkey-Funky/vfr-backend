using Application.Features.Dashboard.DTOs;
using Domain.Entities.Analytics;
using Domain.Enums.Analytics;


namespace Application.Features.Dashboard.Mappings;

public static class DashboardMappings
{
    /// <summary>Maps ActivityEvent domain entity → ActivityEventDto.</summary>
    public static ActivityEventDto ToDto(this Domain.Entities.Analytics.ActivityEvent e)
        => new(
            Id: e.Id,
            EventType: e.EventType,
            ResourceId: e.ResourceId,
            EventData: e.EventData,   // ← FIX: ActivityEvent.EventData (JSONB field, never Description)
            CreatedAt: e.CreatedAt
        );

    /// <summary>Maps Report domain entity → ReportStatusDto.</summary>
    public static ReportStatusDto ToStatusDto(this Report report)
        => new(
            ReportId: report.Id,
            Status: report.Status.ToString(),
            ReportUrl: report.ReportUrl,
            FailureReason: report.FailureReason,
            CreatedAt: report.CreatedAt,
            CompletedAt: report.CompletedAt
        );

    /// <summary>
    /// Formats a DateOnly into a chart label string appropriate for the given grouping.
    /// Day  → "2024-01-15"
    /// Week → "2024-W03"
    /// Month→ "2024-01"
    /// </summary>
    public static string ToChartLabel(this DateOnly date, ChartGroupBy groupBy)
        => groupBy switch
        {
            ChartGroupBy.Day => date.ToString("yyyy-MM-dd"),
            ChartGroupBy.Week => $"{date.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)):D2}",
            ChartGroupBy.Month => date.ToString("yyyy-MM"),
            _ => date.ToString("yyyy-MM-dd")
        };
}