using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Analytics;
using Domain.Enums.Orders;
using Domain.Enums.Product;
using Domain.Enums.Subscription;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted service that runs once per day at midnight UTC.
/// Creates a DashboardSnapshot for every active retailer (covering the day that
/// just ended) and clears the dashboard cache.
///
/// IServiceScopeFactory is used to resolve scoped services (IApplicationDbContext, ICacheService)
/// from a singleton hosted service — never inject scoped services directly into singletons.
///
/// Per-retailer error isolation: a failure for one retailer is logged and skipped;
/// it does NOT abort the batch for other retailers.
///
/// BUGFIX (KPIs always zero for historical dates): previously this job inserted a
/// DashboardSnapshot row with every aggregate hard-coded to zero and never filled
/// it in afterwards. DashboardRepository.GetKpisAsync (and the CSV export / report
/// jobs) read historical KPI numbers directly from these snapshot rows, so every
/// past date silently reported 0 revenue, 0 profit, 0 orders, 0 try-ons and 0
/// low-stock count. This job now actually aggregates the real data for the day
/// it snapshots, the same way DashboardRepository does for "today".
///
/// BUGFIX (off-by-one snapshot date): the job runs right after midnight UTC, so
/// DateTime.UtcNow.Date at that moment is the day that just STARTED, not the day
/// that just ENDED. The snapshot must cover the day that just finished (see the
/// "yesterday's date" contract documented on DashboardSnapshot.SnapshotDate), so
/// the snapshot date is now computed as UtcNow.AddDays(-1).
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

        // BUGFIX: snapshotDate must be the day that just ENDED (the job runs right
        // after midnight UTC, when UtcNow.Date is already the NEW day). Using
        // AddDays(-1) restores the documented "yesterday's date" contract.
        DateOnly snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        DateTime dayStart = snapshotDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime dayEnd = dayStart.AddDays(1);

        int succeeded = 0;
        int failed = 0;

        foreach (var retailerId in retailerIds)
        {
            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                // Upsert semantics — skip if this day's snapshot already exists.
                bool exists = await context.DashboardSnapshots
                    .AnyAsync(
                        s => s.RetailerId == retailerId
                          && s.SnapshotDate == snapshotDate,
                        stoppingToken);

                if (!exists)
                {
                    DashboardSnapshot snapshot = await BuildSnapshotAsync(
                        context, retailerId, snapshotDate, dayStart, dayEnd, stoppingToken);

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

    /// <summary>
    /// Aggregates the real KPI values for <paramref name="retailerId"/> over
    /// [<paramref name="dayStart"/>, <paramref name="dayEnd"/>) and builds the
    /// DashboardSnapshot to persist. Mirrors the same business rules used by
    /// DashboardRepository for "today" so historical and live numbers agree:
    ///   • Revenue = sum of Delivered orders' TotalAmount for the day (by OrderDate).
    ///   • Profit  = Revenue × (1 − active subscription's CommissionRate).
    ///   • Orders  = count of all orders placed that day (by OrderDate), any status.
    ///   • ActiveProducts / LowStockCount = current point-in-time counts
    ///     (these are state snapshots, not day-bound historical facts).
    ///   • ConversionRate / TryOnEngagement = derived from that day's TryOnSessions.
    /// </summary>
    private static async Task<DashboardSnapshot> BuildSnapshotAsync(
        IApplicationDbContext context,
        Guid retailerId,
        DateOnly snapshotDate,
        DateTime dayStart,
        DateTime dayEnd,
        CancellationToken cancellationToken)
    {
        decimal totalRevenue = await context.Orders
            .AsNoTracking()
            .Where(o =>
                o.RetailerId == retailerId &&
                o.Status == OrderStatus.Delivered &&
                o.OrderDate >= dayStart &&
                o.OrderDate < dayEnd)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        int totalOrders = await context.Orders
            .AsNoTracking()
            .Where(o =>
                o.RetailerId == retailerId &&
                o.OrderDate >= dayStart &&
                o.OrderDate < dayEnd)
            .CountAsync(cancellationToken);

        decimal commissionRate = await context.Subscriptions
            .AsNoTracking()
            .Where(s => s.RetailerId == retailerId && s.Status == SubscriptionStatus.Active)
            .Join(context.SubscriptionPlans,
                  s => s.PlanId,
                  p => p.Id,
                  (s, p) => p.CommissionRate)
            .FirstOrDefaultAsync(cancellationToken);

        decimal totalProfit = Math.Round(totalRevenue * (1m - commissionRate), 2);

        int activeProducts = await context.Products
            .AsNoTracking()
            .Where(p => p.RetailerId == retailerId && p.Status == ProductStatus.Active)
            .CountAsync(cancellationToken);

        int lowStockCount = await context.InventoryRecords
            .AsNoTracking()
            .Where(r => r.RetailerId == retailerId && r.Status == InventoryStatus.LowStock)
            .CountAsync(cancellationToken);

        int totalSessions = await context.TryOnSessions
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == retailerId &&
                s.CreatedAt >= dayStart &&
                s.CreatedAt < dayEnd)
            .CountAsync(cancellationToken);

        int convertedSessions = totalSessions == 0
            ? 0
            : await context.TryOnSessions
                .AsNoTracking()
                .Where(s =>
                    s.RetailerId == retailerId &&
                    s.CreatedAt >= dayStart &&
                    s.CreatedAt < dayEnd &&
                    s.ResultedInPurchase)
                .CountAsync(cancellationToken);

        decimal conversionRate = totalSessions == 0
            ? 0m
            : Math.Round((decimal)convertedSessions / totalSessions, 4);

        return new DashboardSnapshot(
            retailerId: retailerId,
            snapshotDate: snapshotDate,
            totalRevenue: totalRevenue,
            totalProfit: totalProfit,
            totalOrders: totalOrders,
            activeProducts: activeProducts,
            lowStockCount: lowStockCount,
            conversionRate: conversionRate,
            tryOnEngagement: totalSessions);
    }
}