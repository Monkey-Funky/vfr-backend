using Application.Features.Dashboard.DTOs;
using Domain.Enums.Analytics;


namespace Application.Interfaces.Persistence;

/// <summary>
/// Specialised repository for Dashboard analytics queries that require raw SQL
/// or PostgreSQL-specific DATE_TRUNC aggregations that cannot be expressed cleanly
/// with LINQ alone.
///
/// All methods enforce retailer isolation: every query filters WHERE retailer_id = @retailerId.
/// </summary>
public interface IDashboardRepository
{
    // ── KPI aggregations ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns daily KPIs aggregated over the requested date range.
    /// Combines DashboardSnapshot records for historical dates with a live query for today.
    /// </summary>
    Task<KpiDto> GetKpisAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    // ── Chart data ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sum of total_amount for Delivered orders, grouped by the requested granularity.
    /// Uses DATE_TRUNC in PostgreSQL. Only Delivered orders count as revenue.
    /// </summary>
    Task<List<ChartDataPoint>> GetRevenueChartAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revenue minus commission deductions (revenue * commission_rate from active subscription plan).
    /// Grouped by the requested granularity.
    /// </summary>
    Task<List<ChartDataPoint>> GetProfitChartAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count of TryOnSessions per day, grouped by requested granularity.
    /// </summary>
    Task<List<ChartDataPoint>> GetSessionsChartAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default);

    // ── Real-time activity ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the 20 most recent ActivityEvents for the retailer.
    /// NO CACHE. Uses the (retailer_id, created_at DESC) index.
    /// </summary>
    Task<List<ActivityEventDto>> GetRealTimeActivityAsync(
        Guid retailerId,
        CancellationToken cancellationToken = default);

    // ── Return analytics ─────────────────────────────────────────────────────

    /// <summary>Count of each ReturnReasonType within the date range.</summary>
    Task<List<ReturnReasonDto>> GetReturnReasonsAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Fit accuracy percentage and counts within the date range.</summary>
    Task<FitAccuracyDto> GetFitAccuracyAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Distribution of ActualSize values from FitAccuracy records.</summary>
    Task<List<SizeDistributionDto>> GetSizeDistributionAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Return count / order count per product within the date range.</summary>
    Task<List<ReturnRateByProductDto>> GetReturnRateByProductAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    // ── Conversion & engagement ──────────────────────────────────────────────

    /// <summary>
    /// Conversion rate = sessions where ResultedInPurchase=true / total sessions.
    /// Returns 0 safely when no sessions exist.
    /// </summary>
    Task<ConversionRateDto> GetConversionRateAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>Sessions grouped by day (or ProductId feature breakdowns) within the range.</summary>
    Task<List<TryOnEngagementDto>> GetTryOnEngagementAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default);

    // ── Export ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Streams all dashboard data rows for CSV export.
    /// Uses IAsyncEnumerable to avoid loading the full result set into memory.
    /// </summary>
    IAsyncEnumerable<DashboardExportRow> StreamExportRowsAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}