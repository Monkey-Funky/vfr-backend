using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Analytics;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted service that runs once per day at midnight UTC.
/// Creates a DashboardSnapshot for every active retailer and clears dashboard cache.
///
/// IServiceScopeFactory is used to resolve scoped services (IApplicationDbContext, ICacheService)
/// from a singleton hosted service — never inject scoped services directly into singletons.
///
/// Per-retailer error isolation: a failure for one retailer is logged and skipped;
/// it does NOT abort the batch for other retailers.
/// </summary>
public sealed class DashboardSnapshotJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DashboardSnapshotJob> _logger;

    public DashboardSnapshotJob(
        IServiceScopeFactory scopeFactory,
        ILogger<DashboardSnapshotJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DashboardSnapshotJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Compute delay until next midnight UTC.
            var now = DateTime.UtcNow;
            var nextMidnight = now.Date.AddDays(1);   // DateTime (midnight tomorrow)
            var delay = nextMidnight - now;

            _logger.LogInformation(
                "DashboardSnapshotJob: next run at {NextMidnight} UTC (in {Delay:hh\\:mm\\:ss}).",
                nextMidnight, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSnapshotBatchAsync(stoppingToken);
        }

        _logger.LogInformation("DashboardSnapshotJob stopped.");
    }

    private async Task RunSnapshotBatchAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "DashboardSnapshotJob: starting snapshot batch at {UtcNow}.", DateTime.UtcNow);

        await using var scope = _scopeFactory.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        // ── Fetch all non-deleted retailer IDs ───────────────────────────────
        // FIX-3: RetailerAccount does not have IsActive — removed. Only !IsDeleted.
        List<Guid> retailerIds;
        try
        {
            retailerIds = await context.RetailerAccounts
                .AsNoTracking()
                .Where(r => !r.IsDeleted)
                .Select(r => r.Id)
                .ToListAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "DashboardSnapshotJob: failed to load retailer IDs. Aborting batch.");
            return;
        }

        _logger.LogInformation(
            "DashboardSnapshotJob: processing {Count} retailer(s).", retailerIds.Count);

        // FIX-1: snapshotDate is DateOnly so that DashboardSnapshot.SnapshotDate
        //         (also DateOnly) comparison compiles without operator '==' mismatch.
        DateOnly snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow);

        int succeeded = 0;
        int failed = 0;

        foreach (var retailerId in retailerIds)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                // Upsert semantics — skip if today's snapshot already exists.
                bool exists = await context.DashboardSnapshots
                    .AnyAsync(
                        s => s.RetailerId == retailerId
                          && s.SnapshotDate == snapshotDate,   // FIX-1: both are DateOnly
                        stoppingToken);

                if (!exists)
                {
                    // FIX-2: No static Create() on DashboardSnapshot — use the public constructor.
                    // Initial values are zero; DashboardSnapshotJob only marks the record as
                    // "exists for today". Actual KPI aggregation happens in DashboardRepository.
                    var snapshot = new DashboardSnapshot(
                        retailerId: retailerId,
                        snapshotDate: snapshotDate,
                        totalRevenue: 0m,
                        totalProfit: 0m,
                        totalOrders: 0,
                        activeProducts: 0,
                        lowStockCount: 0,
                        conversionRate: 0m,
                        tryOnEngagement: 0);

                    context.DashboardSnapshots.Add(snapshot);
                    await context.SaveChangesAsync(stoppingToken);
                }

                // Clear all dashboard cache keys for this retailer.
                await cacheService.RemoveByPatternAsync(
                    $"dashboard:{retailerId}:*", stoppingToken);

                succeeded++;
            }
            catch (Exception ex)
            {
                // Per-retailer isolation — failure does NOT abort the batch.
                _logger.LogError(ex,
                    "DashboardSnapshotJob: failed for Retailer {RetailerId}. " +
                    "Continuing with remaining retailers.",
                    retailerId);
                failed++;
            }
        }

        _logger.LogInformation(
            "DashboardSnapshotJob: batch complete. Succeeded={Succeeded}, Failed={Failed}.",
            succeeded, failed);
    }
}