using Application.Features.Dashboard.Commands.GenerateReport;
using Application.Features.Dashboard.DTOs;
using Application.Features.Dashboard.Queries.GetConversionRate;
using Application.Features.Dashboard.Queries.GetDashboardExport;
using Application.Features.Dashboard.Queries.GetFitAccuracy;
using Application.Features.Dashboard.Queries.GetKpis;
using Application.Features.Dashboard.Queries.GetProfitChart;
using Application.Features.Dashboard.Queries.GetRealTimeActivity;
using Application.Features.Dashboard.Queries.GetReportStatus;
using Application.Features.Dashboard.Queries.GetReturnRateByProduct;
using Application.Features.Dashboard.Queries.GetReturnReasons;
using Application.Features.Dashboard.Queries.GetRevenueChart;
using Application.Features.Dashboard.Queries.GetSessionsChart;
using Application.Features.Dashboard.Queries.GetSizeDistribution;
using Application.Features.Dashboard.Queries.GetTryOnEngagement;
using Domain.Enums.Analytics;
using Swashbuckle.AspNetCore.Annotations;
using System.Text;

namespace API.Controllers.Dashboard;


/// <summary>
/// Dashboard analytics endpoints for the authenticated retailer.
/// All 14 GET endpoints accept ?from=yyyy-MM-dd&amp;to=yyyy-MM-dd query parameters.
/// Date range validation returns 422 with descriptive messages via FluentValidation pipeline.
/// All GET responses (except /export) use ApiResponse&lt;T&gt; envelope.
/// POST /reports returns 202 Accepted immediately; poll /reports/{reportId} for status.
/// </summary>
[Route("api/retailers/{retailerId:guid}/dashboard")]
[SwaggerTag("Dashboard — analytics KPIs, charts, return metrics, and async report generation.")]
public sealed class DashboardController : BaseApiController
{
    public DashboardController() { }

    // ── 1. KPIs ──────────────────────────────────────────────────────────────

    [HttpGet("kpis")]
    [SwaggerOperation(
        Summary = "Get dashboard KPIs",
        Description = "Returns aggregate KPI cards (revenue, profit, orders, try-ons, " +
                      "active products, low-stock count, conversion rate, returns, new orders) " +
                      "for the requested date range. Cached for 5 minutes per retailer. " +
                      "Cache is invalidated automatically when an order is delivered.")]
    [ProducesResponseType(typeof(ApiResponse<KpiDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetKpis(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        KpiDto result = await Sender.Send(
            new GetKpisQuery(from, to),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 2. Revenue Chart ─────────────────────────────────────────────────────

    [HttpGet("revenue")]
    [SwaggerOperation(
        Summary = "Get revenue chart data",
        Description = "Returns revenue (Delivered orders only) grouped by Day, Week, or Month " +
                      "for the requested date range. Uses PostgreSQL DATE_TRUNC internally. " +
                      "Cached for 30 minutes per retailer per date range.")]
    [ProducesResponseType(typeof(ApiResponse<List<ChartDataPoint>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetRevenueChart(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] ChartGroupBy groupBy = ChartGroupBy.Day,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<ChartDataPoint> result = await Sender.Send(
            new GetRevenueChartQuery(from, to, groupBy),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 3. Profit Chart ──────────────────────────────────────────────────────

    [HttpGet("profit")]
    [SwaggerOperation(
        Summary = "Get profit chart data",
        Description = "Returns profit (revenue minus platform commission deduction) grouped " +
                      "by Day, Week, or Month. Commission rate is read from the retailer's " +
                      "active subscription plan. Cached for 30 minutes.")]
    [ProducesResponseType(typeof(ApiResponse<List<ChartDataPoint>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetProfitChart(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] ChartGroupBy groupBy = ChartGroupBy.Day,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<ChartDataPoint> result = await Sender.Send(
            new GetProfitChartQuery(from, to, groupBy),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 4. Sessions Chart ────────────────────────────────────────────────────

    [HttpGet("sessions")]
    [SwaggerOperation(
        Summary = "Get virtual try-on sessions chart",
        Description = "Returns the count of virtual try-on sessions grouped by Day, Week, or " +
                      "Month for the requested date range. Cached for 30 minutes.")]
    [ProducesResponseType(typeof(ApiResponse<List<ChartDataPoint>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetSessionsChart(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] ChartGroupBy groupBy = ChartGroupBy.Day,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<ChartDataPoint> result = await Sender.Send(
            new GetSessionsChartQuery(from, to, groupBy),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 5. Real-Time Activity ────────────────────────────────────────────────

    [HttpGet("activity")]
    [SwaggerOperation(
        Summary = "Get real-time activity feed",
        Description = "Returns the 20 most recent activity events for this retailer, ordered by " +
                      "time descending. This endpoint is NEVER cached — it always reads from the " +
                      "database directly to ensure real-time freshness. No date range parameters.")]
    [ProducesResponseType(typeof(ApiResponse<List<ActivityEventDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRealTimeActivity(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<ActivityEventDto> result = await Sender.Send(
            new GetRealTimeActivityQuery(),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 6. Return Reasons ────────────────────────────────────────────────────

    [HttpGet("return-reasons")]
    [SwaggerOperation(
        Summary = "Get return reason distribution",
        Description = "Returns the count and percentage of each return reason category " +
                      "(WrongSize, DefectiveItem, NotAsDescribed, etc.) for the date range. " +
                      "Cached for 1 hour.")]
    [ProducesResponseType(typeof(ApiResponse<List<ReturnReasonDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetReturnReasons(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<ReturnReasonDto> result = await Sender.Send(
            new GetReturnReasonsQuery(from, to),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 7. Fit Accuracy ──────────────────────────────────────────────────────

    [HttpGet("fit-accuracy")]
    [SwaggerOperation(
        Summary = "Get VFR fit accuracy metrics",
        Description = "Returns the total number of VFR size predictions, how many were accurate, " +
                      "and the accuracy percentage for the date range. Cached for 1 hour.")]
    [ProducesResponseType(typeof(ApiResponse<FitAccuracyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetFitAccuracy(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        FitAccuracyDto result = await Sender.Send(
            new GetFitAccuracyQuery(from, to),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 8. Size Distribution ─────────────────────────────────────────────────

    [HttpGet("size-distribution")]
    [SwaggerOperation(
        Summary = "Get size distribution",
        Description = "Returns the distribution of actual sizes chosen by customers during " +
                      "virtual try-ons (derived from FitAccuracy records) for the date range. " +
                      "Cached for 1 hour.")]
    [ProducesResponseType(typeof(ApiResponse<List<SizeDistributionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetSizeDistribution(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<SizeDistributionDto> result = await Sender.Send(
            new GetSizeDistributionQuery(from, to),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 9. Return Rate By Product ────────────────────────────────────────────

    [HttpGet("return-rate")]
    [SwaggerOperation(
        Summary = "Get return rate by product",
        Description = "Returns order count, return count, and return rate percentage per product " +
                      "for the date range, ordered by return rate descending. Cached for 1 hour.")]
    [ProducesResponseType(typeof(ApiResponse<List<ReturnRateByProductDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetReturnRateByProduct(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<ReturnRateByProductDto> result = await Sender.Send(
            new GetReturnRateByProductQuery(from, to),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 10. Conversion Rate ──────────────────────────────────────────────────

    [HttpGet("conversion")]
    [SwaggerOperation(
        Summary = "Get conversion rate",
        Description = "Returns the ratio of virtual try-on sessions that resulted in a purchase " +
                      "for the date range. Returns 0% safely for retailers with no sessions. " +
                      "Cached for 30 minutes.")]
    [ProducesResponseType(typeof(ApiResponse<ConversionRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetConversionRate(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        ConversionRateDto result = await Sender.Send(
            new GetConversionRateQuery(from, to),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 11. Try-On Engagement ────────────────────────────────────────────────

    [HttpGet("engagement")]
    [SwaggerOperation(
        Summary = "Get try-on engagement metrics",
        Description = "Returns session count and average session duration (seconds) grouped by " +
                      "Day, Week, or Month for the date range. Cached for 30 minutes.")]
    [ProducesResponseType(typeof(ApiResponse<List<TryOnEngagementDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetTryOnEngagement(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] ChartGroupBy groupBy = ChartGroupBy.Day,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        List<TryOnEngagementDto> result = await Sender.Send(
            new GetTryOnEngagementQuery(from, to, groupBy),
            cancellationToken);

        return OkResponse(result);
    }

    // ── 12. Dashboard Export ─────────────────────────────────────────────────

    [HttpGet("export")]
    [SwaggerOperation(
        Summary = "Export dashboard data as CSV",
        Description = "Streams all DashboardSnapshot records for the date range as a CSV file. " +
                      "Uses IAsyncEnumerable internally to avoid loading the full result set " +
                      "into memory. Response Content-Type is text/csv. Not cached.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportDashboard(
        Guid retailerId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        IAsyncEnumerable<DashboardExportRow> stream = await Sender.Send(
            new GetDashboardExportQuery(from, to),
            cancellationToken);

        // Stream CSV directly to the response body — no buffering in memory.
        Response.ContentType = "text/csv";
        Response.Headers["Content-Disposition"] =
            $"attachment; filename=\"dashboard-export-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.csv\"";

        await using StreamWriter writer = new(Response.Body, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync(
            "SnapshotDate,TotalRevenue,TotalProfit,TotalOrders," +
            "ActiveProducts,LowStockCount,ConversionRate,TryOnEngagement");

        await foreach (DashboardExportRow row in stream
            .WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(
                $"{row.SnapshotDate:yyyy-MM-dd}," +
                $"{row.TotalRevenue}," +
                $"{row.TotalProfit}," +
                $"{row.TotalOrders}," +
                $"{row.ActiveProducts}," +
                $"{row.LowStockCount}," +
                $"{row.ConversionRate}," +
                $"{row.TryOnEngagement}");
        }

        await writer.FlushAsync(cancellationToken);

        // Return EmptyResult — response body has already been written.
        return new EmptyResult();
    }

    // ── 13. Generate Report (async, 202) ─────────────────────────────────────

    [HttpPost("reports")]
    [SwaggerOperation(
        Summary = "Request an async dashboard report",
        Description = "Creates a report generation job for the requested date range and returns " +
                      "202 Accepted immediately with a reportId. The report is generated " +
                      "asynchronously by ReportGenerationJob. Poll GET /reports/{reportId} " +
                      "to check the status and retrieve the download URL when ready.")]
    [ProducesResponseType(typeof(ApiResponse<GenerateReportResponse>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GenerateReport(
        Guid retailerId,
        [FromBody] GenerateReportRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<GenerateReportResponse> result = await Sender.Send(
            new GenerateReportCommand(request.From, request.To),
            cancellationToken);

        // 202 Accepted — not 201 Created.
        // The resource does not yet exist in its final form (status=Pending).
        // No Location header is set here; clients use the returned reportId to poll.
        return Accepted(new ApiResponse<GenerateReportResponse>
        {
            Success = true,
            Data = result.Data
        });
    }

    // ── 14. Get Report Status ────────────────────────────────────────────────

    [HttpGet("reports/{reportId:guid}", Name = "GetReportStatus")]
    [SwaggerOperation(
        Summary = "Poll report generation status",
        Description = "Returns the current status of an async report generation job. " +
                      "Status values: Pending → Processing → Ready | Failed. " +
                      "When Status=Ready, ReportUrl contains a pre-signed S3 URL valid for 7 days. " +
                      "Returns 404 if the reportId does not exist or belongs to a different retailer.")]
    [ProducesResponseType(typeof(ApiResponse<ReportStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReportStatus(
        Guid retailerId,
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        ReportStatusDto result = await Sender.Send(
            new GetReportStatusQuery(reportId),
            cancellationToken);

        return OkResponse(result);
    }
}

/// <summary>
/// Request body for POST /api/retailers/{retailerId}/dashboard/reports.
/// Validation is performed by GenerateReportCommandValidator (FluentValidation pipeline).
/// </summary>
public sealed record GenerateReportRequest(
    DateOnly From,
    DateOnly To
);