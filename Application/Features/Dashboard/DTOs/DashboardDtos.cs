

namespace Application.Features.Dashboard.DTOs;

// ── KPI ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Aggregate KPI summary for the dashboard header cards.
/// Covers the requested date range.
/// </summary>
public sealed record KpiDto(
    int TotalOrders,
    decimal TotalRevenue,
    decimal TotalProfit,
    int TotalTryOns,
    int ActiveProducts,
    int LowStockCount,
    decimal ConversionRate,
    int TotalReturns,
    int NewOrders
);

public sealed record RevenueDataPointDto(
    DateTime PeriodStart,
    decimal Revenue,
    int OrderCount);


// ── Charts ───────────────────────────────────────────────────────────────────

/// <summary>Single data point for line/bar chart rendering.</summary>
public sealed record ChartDataPoint(
    string Label,   // ISO date string e.g. "2024-01-15" or "2024-W03"
    decimal Value
);
// ── Activity ──────────────────────────────────────────────────────────────────

/// <summary>
/// ActivityEvent DTO. EventData is the JSONB metadata from ActivityEvent.EventData.
/// Named EventData (not Description) to match the domain entity property exactly.
/// </summary>
public sealed record ActivityEventDto(
    Guid Id,
    string EventType,
    Guid? ResourceId,
    string EventData,
    DateTime CreatedAt
);

// ── Return analytics ─────────────────────────────────────────────────────────

public sealed record ReturnReasonDto(
    string Reason,
    int Count,
    double Percentage
);

public sealed record FitAccuracyDto(
    int TotalPredictions,
    int AccuratePredictions,
    double AccuracyPercentage
);

public sealed record SizeDistributionDto(
    string Size,
    int Count,
    double Percentage
);
public sealed record ReturnRateByProductDto(
    Guid ProductId,
    string ProductName,
    int TotalOrders,
    int TotalReturns,
    double ReturnRatePercentage
);

// ── Conversion & engagement ──────────────────────────────────────────────────

public sealed record ConversionRateDto(
    int TotalSessions,
    int ConvertedSessions,
    double ConversionRatePercentage
);

public sealed record TryOnEngagementDto(
    string Label,
    int SessionCount,
    double AvgDurationSeconds
);

// ── Export ───────────────────────────────────────────────────────────────────

public sealed record DashboardExportRow(
    DateOnly SnapshotDate,
    decimal TotalRevenue,
    decimal TotalProfit,
    int TotalOrders,
    int ActiveProducts,
    int LowStockCount,
    decimal ConversionRate,
    int TryOnEngagement
);


// ── Report status ─────────────────────────────────────────────────────────────

public sealed record ReportStatusDto(
    Guid ReportId,
    string Status,
    string? ReportUrl,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

/// <summary>Response body for POST /reports → 202 Accepted.</summary>
public sealed record GenerateReportResponse(Guid ReportId);
