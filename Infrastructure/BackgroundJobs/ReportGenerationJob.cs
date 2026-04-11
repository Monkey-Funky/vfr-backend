using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Analytics;
using Microsoft.Extensions.Hosting;


namespace Infrastructure.BackgroundJobs;
/// <summary>
/// Long-running BackgroundService that consumes report generation requests
/// from IReportQueue, builds a PDF, uploads it to S3, and updates the Report record.
///
/// SCHEDULE: Continuous — processes requests as they arrive via Channel.ReadAllAsync.
/// DISTRIBUTED LOCK: Not required — channel is in-process and bounded; only one instance
///   processes each report ID because Channel has SingleReader = true.
/// FAILURE BEHAVIOUR: On exception for a specific report, marks it Failed, logs Error,
///   and continues to the next report. Host-level exception → log Fatal, host continues.
/// </summary>
public sealed class ReportGenerationJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReportQueue _reportQueue;
    private readonly ILogger<ReportGenerationJob> _logger;

    public ReportGenerationJob(
        IServiceScopeFactory scopeFactory,
        IReportQueue reportQueue,
        ILogger<ReportGenerationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _reportQueue = reportQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Job} hosted service started.", nameof(ReportGenerationJob));

        try
        {
            await foreach (Guid reportId in _reportQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessReportAsync(reportId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw; // propagate graceful shutdown
                }
                catch (Exception ex)
                {
                    // Per-report failure — mark failed and continue.
                    _logger.LogError(ex,
                        "{Job} failed to generate ReportId {ReportId}.",
                        nameof(ReportGenerationJob), reportId);

                    await TryMarkReportFailedAsync(reportId, ex.Message, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("{Job} stopped gracefully.", nameof(ReportGenerationJob));
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "{Job} encountered a fatal unhandled error.",
                nameof(ReportGenerationJob));
        }
    }

    // ── Report processing ─────────────────────────────────────────────────────

    private async Task ProcessReportAsync(Guid reportId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        IApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        IS3StorageService s3 =
            scope.ServiceProvider.GetRequiredService<IS3StorageService>();

        Report? report = await context.Reports
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

        if (report is null)
        {
            _logger.LogWarning(
                "{Job} received unknown ReportId {ReportId}. Skipping.",
                nameof(ReportGenerationJob), reportId);
            return;
        }

        report.MarkProcessing();
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Job} generating report for ReportId {ReportId} | RetailerId {RetailerId}",
            nameof(ReportGenerationJob), reportId, report.RetailerId);

        using MemoryStream content = await BuildReportCsvAsync(
            report, context, cancellationToken);

        string s3Key = $"reports/{report.RetailerId}/{reportId}.csv";
        string reportUrl = await s3.UploadReportAsync(
            s3Key, content, "text/csv", cancellationToken);

        report.MarkReady(reportUrl);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Job} completed ReportId {ReportId}. URL: {Url}",
            nameof(ReportGenerationJob), reportId, reportUrl);
    }

    private static async Task<MemoryStream> BuildReportCsvAsync(
        Report report,
        IApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        MemoryStream stream = new();
        await using StreamWriter writer = new(stream, leaveOpen: true);

        await writer.WriteLineAsync(
            "SnapshotDate,TotalRevenue,TotalProfit,TotalOrders," +
            "ActiveProducts,LowStockCount,ConversionRate,TryOnEngagement");

        List<DashboardSnapshot> snapshots = await context.DashboardSnapshots
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == report.RetailerId &&
                s.SnapshotDate >= report.RangeFrom &&
                s.SnapshotDate <= report.RangeTo)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        foreach (DashboardSnapshot snapshot in snapshots)
        {
            await writer.WriteLineAsync(
                $"{snapshot.SnapshotDate:yyyy-MM-dd}," +
                $"{snapshot.TotalRevenue}," +
                $"{snapshot.TotalProfit}," +
                $"{snapshot.TotalOrders}," +
                $"{snapshot.ActiveProducts}," +
                $"{snapshot.LowStockCount}," +
                $"{snapshot.ConversionRate}," +
                $"{snapshot.TryOnEngagement}");
        }

        await writer.FlushAsync(cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private async Task TryMarkReportFailedAsync(
        Guid reportId,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IApplicationDbContext context =
                scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            Report? report = await context.Reports
                .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);

            if (report is not null)
            {
                report.MarkFailed(reason);
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Job} failed to mark ReportId {ReportId} as Failed.",
                nameof(ReportGenerationJob), reportId);
        }
    }
}