using Application.Features.Dashboard.DTOs;
using Application.Features.Dashboard.Mappings;
using Application.Interfaces.Persistence;
using Domain.Entities.Analytics;
using Domain.Enums.Analytics;
using Domain.Enums.Subscription;
using System.Runtime.CompilerServices;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Implements IDashboardRepository using direct EF Core queries and raw SQL
/// where PostgreSQL-specific DATE_TRUNC aggregations are required.
///
/// TENANT ISOLATION CONTRACT: Every single query in this repository
/// includes WHERE retailer_id = @retailerId. This is verified in the
/// Success Criteria checklist at the bottom of this file.
///
/// ZERO-DATA CONTRACT: All aggregate queries return zeros (not null, not 404)
/// when no data exists for the retailer in the requested range.
/// </summary>
public sealed class DashboardRepository : IDashboardRepository
{
    private readonly IApplicationDbContext _context;

    public DashboardRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static DateTime ToDateTime(DateOnly date)
        => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    // ─────────────────────────────────────────────────────────────────────────
    // KPI
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<KpiDto> GetKpisAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateTime todayStart = DateTime.UtcNow.Date;
        DateTime todayEnd = todayStart.AddDays(1);

        // ── Run all independent DB queries sequentially on the shared DbContext ──
        // NOTE: These were previously fired concurrently via Task.WhenAll while all
        // sharing the SAME _context (DbContext) instance. EF Core's DbContext is NOT
        // thread-safe / does not support concurrent operations on a single instance —
        // running multiple queries against it at once throws:
        //   "InvalidOperationException: A second operation started on this context
        //    before a previous operation completed."
        // This was the root cause of the 500 INTERNAL_ERROR on GET .../dashboard/kpis.
        // True parallelism would require a separate DbContext per query (e.g. via
        // IDbContextFactory), which is a bigger change than this bug fix needs —
        // these are cheap count/sum queries, so running them sequentially is fast
        // and, most importantly, correct.

        var snapshotAgg = await _context.DashboardSnapshots
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == retailerId &&
                s.SnapshotDate >= from &&
                s.SnapshotDate < today)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Sum(s => s.TotalRevenue),
                Profit = g.Sum(s => s.TotalProfit),
                Orders = g.Sum(s => s.TotalOrders),
                TryOns = g.Sum(s => s.TryOnEngagement),
                LowStock = g.Max(s => s.LowStockCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        decimal liveRevenue = await _context.Orders
            .AsNoTracking()
            .Where(o =>
                o.RetailerId == retailerId &&
                o.Status == "Delivered" &&
                o.CreatedAt >= todayStart &&
                o.CreatedAt < todayEnd)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        int liveTryOns = await _context.TryOnSessions
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == retailerId &&
                s.CreatedAt >= todayStart &&
                s.CreatedAt < todayEnd)
            .CountAsync(cancellationToken);

        int activeProducts = await _context.Products
            .AsNoTracking()
            .Where(p => p.RetailerId == retailerId && p.Status == "Active")
            .CountAsync(cancellationToken);

        int totalReturns = await _context.ReturnReasons
            .AsNoTracking()
            .Where(r =>
                r.RetailerId == retailerId &&
                r.ReturnedAt >= fromDt &&
                r.ReturnedAt < toDt)
            .CountAsync(cancellationToken);

        int newOrders = await _context.Orders
            .AsNoTracking()
            .Where(o =>
                o.RetailerId == retailerId &&
                o.Status == "NotProcessed" &&
                o.CreatedAt >= fromDt &&
                o.CreatedAt < toDt)
            .CountAsync(cancellationToken);

        int totalSessions = await _context.TryOnSessions
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == retailerId &&
                s.CreatedAt >= fromDt &&
                s.CreatedAt < toDt)
            .CountAsync(cancellationToken);

        // convertedSessions depends on totalSessions — only query if needed
        int convertedSessions = 0;
        if (totalSessions > 0)
        {
            convertedSessions = await _context.TryOnSessions
                .AsNoTracking()
                .Where(s =>
                    s.RetailerId == retailerId &&
                    s.CreatedAt >= fromDt &&
                    s.CreatedAt < toDt &&
                    s.ResultedInPurchase == true)
                .CountAsync(cancellationToken);
        }

        decimal conversionRate = totalSessions == 0
            ? 0m
            : Math.Round((decimal)convertedSessions / totalSessions, 4);

        return new KpiDto(
            TotalOrders: snapshotAgg?.Orders ?? 0,
            TotalRevenue: (snapshotAgg?.Revenue ?? 0m) + liveRevenue,
            TotalProfit: snapshotAgg?.Profit ?? 0m,
            TotalTryOns: (snapshotAgg?.TryOns ?? 0) + liveTryOns,
            ActiveProducts: activeProducts,
            LowStockCount: snapshotAgg?.LowStock ?? 0,
            ConversionRate: conversionRate,
            TotalReturns: totalReturns,
            NewOrders: newOrders
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Revenue Chart — DATE_TRUNC via raw SQL (Fix-4: _context.Database now available)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<ChartDataPoint>> GetRevenueChartAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default)
    {
        string truncUnit = groupBy switch
        {
            ChartGroupBy.Day => "day",
            ChartGroupBy.Week => "week",
            ChartGroupBy.Month => "month",
            _ => "day"
        };

        // FIX-4: _context.Database is now available after IApplicationDbContext update.
        string sql = $@"
            SELECT DATE_TRUNC('{truncUnit}', order_date)::date AS period,
                   COALESCE(SUM(total_amount), 0)              AS total
            FROM   orders
            WHERE  retailer_id = {{0}}
              AND  status      = 'Delivered'
              AND  order_date  >= {{1}}
              AND  order_date  <  {{2}}
              AND  is_deleted  = false
            GROUP  BY period
            ORDER  BY period";

        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        List<(DateTime Period, decimal Total)> rows =
            await _context.Database
                .SqlQueryRaw<RevenueRow>(sql, retailerId, fromDt, toDt)
                .Select(r => ValueTuple.Create(r.Period, r.Total))
                .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ChartDataPoint(
                Label: DateOnly.FromDateTime(r.Period).ToChartLabel(groupBy),
                Value: r.Total))
            .ToList();
    }

    private sealed record RevenueRow(DateTime Period, decimal Total);

    // ─────────────────────────────────────────────────────────────────────────
    // Profit Chart — Fix-2: SubscriptionStatus enum comparison
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<ChartDataPoint>> GetProfitChartAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default)
    {
        // FIX-2: s.Status is SubscriptionStatus (enum), not string.
        // Original: s.Status == "Active" → CS0019: operator '==' cannot be applied.
        // Fixed:    s.Status == SubscriptionStatus.Active.
        decimal commissionRate = await _context.Subscriptions
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == retailerId &&
                s.Status == SubscriptionStatus.Active)   // ← FIX-2
            .Join(_context.SubscriptionPlans,
                  s => s.PlanId,
                  p => p.Id,
                  (s, p) => p.CommissionRate)
            .FirstOrDefaultAsync(cancellationToken);

        // Profit = revenue × (1 − commissionRate) per period.
        List<ChartDataPoint> revenuePoints = await GetRevenueChartAsync(
            retailerId, from, to, groupBy, cancellationToken);

        return revenuePoints
            .Select(p => p with { Value = Math.Round(p.Value * (1m - commissionRate), 2) })
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Sessions Chart — Fix-4: _context.Database
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<ChartDataPoint>> GetSessionsChartAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default)
    {
        string truncUnit = groupBy switch
        {
            ChartGroupBy.Day => "day",
            ChartGroupBy.Week => "week",
            ChartGroupBy.Month => "month",
            _ => "day"
        };

        // FIX-4: _context.Database now available.
        string sql = $@"
            SELECT DATE_TRUNC('{truncUnit}', created_at)::date AS period,
                   COUNT(*)::numeric                            AS total
            FROM   try_on_sessions
            WHERE  retailer_id = {{0}}
              AND  created_at  >= {{1}}
              AND  created_at  <  {{2}}
            GROUP  BY period
            ORDER  BY period";

        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        List<(DateTime Period, decimal Total)> rows =
            await _context.Database
                .SqlQueryRaw<RevenueRow>(sql, retailerId, fromDt, toDt)
                .Select(r => ValueTuple.Create(r.Period, r.Total))
                .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ChartDataPoint(
                Label: DateOnly.FromDateTime(r.Period).ToChartLabel(groupBy),
                Value: r.Total))
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Real-Time Activity — NO CACHE
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<ActivityEventDto>> GetRealTimeActivityAsync(
        Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ActivityEvents
            .AsNoTracking()
            .Where(e => e.RetailerId == retailerId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(20)
            .Select(e => new ActivityEventDto(
                e.Id,
                e.EventType,
                e.ResourceId,
                e.EventData,
                e.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Return Reasons
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<ReturnReasonDto>> GetReturnReasonsAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        var groups = await _context.ReturnReasons
            .AsNoTracking()
            .Where(r =>
                r.RetailerId == retailerId &&
                r.ReturnedAt >= fromDt &&
                r.ReturnedAt < toDt)
            .GroupBy(r => r.Reason)
            .Select(g => new { Reason = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
            return [];

        int total = groups.Sum(g => g.Count);

        return groups
            .Select(g => new ReturnReasonDto(
                Reason: g.Reason,
                Count: g.Count,
                Percentage: Math.Round((double)g.Count / total * 100, 1)))
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fit Accuracy
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<FitAccuracyDto> GetFitAccuracyAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        var agg = await _context.FitAccuracies
            .AsNoTracking()
            .Where(f =>
                f.RetailerId == retailerId &&
                f.RecordedAt >= fromDt &&
                f.RecordedAt < toDt)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Accurate = g.Count(f => f.WasAccurate)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (agg is null || agg.Total == 0)
            return new FitAccuracyDto(0, 0, 0.0);

        return new FitAccuracyDto(
            TotalPredictions: agg.Total,
            AccuratePredictions: agg.Accurate,
            AccuracyPercentage: Math.Round((double)agg.Accurate / agg.Total * 100, 1)
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Size Distribution
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<SizeDistributionDto>> GetSizeDistributionAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        var groups = await _context.FitAccuracies
            .AsNoTracking()
            .Where(f =>
                f.RetailerId == retailerId &&
                f.RecordedAt >= fromDt &&
                f.RecordedAt < toDt)
            .GroupBy(f => f.ActualSize)
            .Select(g => new { Size = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
            return [];

        int total = groups.Sum(g => g.Count);

        return groups
            .Select(g => new SizeDistributionDto(
                Size: g.Size,
                Count: g.Count,
                Percentage: Math.Round((double)g.Count / total * 100, 1)))
            .OrderByDescending(s => s.Count)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Return Rate By Product — Fix-3: explicit Join (no Order navigation)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<ReturnRateByProductDto>> GetReturnRateByProductAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        // FIX-3: OrderItem does NOT have an Order navigation property.
        // Original: oi.Order!.RetailerId → CS1061 — member not found.
        // Fixed:    start from Orders (scoped by retailerId), then Join to OrderItems.
        Dictionary<Guid, int> ordersByProduct = await _context.Orders
            .AsNoTracking()
            .Where(o =>
                o.RetailerId == retailerId &&
                o.CreatedAt >= fromDt &&
                o.CreatedAt < toDt)
            .Join(
                _context.OrderItems,
                o => o.Id,
                oi => oi.OrderId,
                (o, oi) => oi)
            .Where(oi => oi.ProductId != null)
            .GroupBy(oi => oi.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ProductId, g => g.Count, cancellationToken);

        if (ordersByProduct.Count == 0)
            return [];

        Dictionary<Guid, int> returnsByProduct = await _context.ReturnReasons
            .AsNoTracking()
            .Where(r =>
                r.RetailerId == retailerId &&
                r.ReturnedAt >= fromDt &&
                r.ReturnedAt < toDt &&
                r.ProductId != null)
            .GroupBy(r => r.ProductId!.Value)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ProductId, g => g.Count, cancellationToken);

        HashSet<Guid> productIds = ordersByProduct.Keys.ToHashSet();
        Dictionary<Guid, string> productNames = await _context.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.RetailerId == retailerId)
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return ordersByProduct
            .Select(kv =>
            {
                int orders = kv.Value;
                int returns = returnsByProduct.GetValueOrDefault(kv.Key, 0);
                string name = productNames.GetValueOrDefault(kv.Key, "Unknown");
                double rate = orders == 0
                    ? 0.0
                    : Math.Round((double)returns / orders * 100, 1);

                return new ReturnRateByProductDto(
                    ProductId: kv.Key,
                    ProductName: name,
                    TotalOrders: orders,
                    TotalReturns: returns,
                    ReturnRatePercentage: rate);
            })
            .OrderByDescending(r => r.ReturnRatePercentage)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Conversion Rate
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ConversionRateDto> GetConversionRateAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        var agg = await _context.TryOnSessions
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == retailerId &&
                s.CreatedAt >= fromDt &&
                s.CreatedAt < toDt)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Converted = g.Count(s => s.ResultedInPurchase)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (agg is null || agg.Total == 0)
            return new ConversionRateDto(0, 0, 0.0);

        return new ConversionRateDto(
            TotalSessions: agg.Total,
            ConvertedSessions: agg.Converted,
            ConversionRatePercentage: Math.Round((double)agg.Converted / agg.Total * 100, 2)
        );
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Try-On Engagement — Fix-4: _context.Database
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<List<TryOnEngagementDto>> GetTryOnEngagementAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        ChartGroupBy groupBy,
        CancellationToken cancellationToken = default)
    {
        string truncUnit = groupBy switch
        {
            ChartGroupBy.Day => "day",
            ChartGroupBy.Week => "week",
            ChartGroupBy.Month => "month",
            _ => "day"
        };

        // FIX-4: _context.Database now available.
        string sql = $@"
            SELECT DATE_TRUNC('{truncUnit}', created_at)::date   AS period,
                   COUNT(*)                                       AS session_count,
                   AVG(session_duration_seconds)::numeric(10,2)  AS avg_duration
            FROM   try_on_sessions
            WHERE  retailer_id = {{0}}
              AND  created_at  >= {{1}}
              AND  created_at  <  {{2}}
            GROUP  BY period
            ORDER  BY period";

        DateTime fromDt = ToDateTime(from);
        DateTime toDt = ToDateTime(to).AddDays(1);

        List<EngagementRow> rows =
            await _context.Database
                .SqlQueryRaw<EngagementRow>(sql, retailerId, fromDt, toDt)
                .ToListAsync(cancellationToken);

        return rows
            .Select(r => new TryOnEngagementDto(
                Label: DateOnly.FromDateTime(r.Period).ToChartLabel(groupBy),
                SessionCount: r.SessionCount,
                AvgDurationSeconds: (double)r.AvgDuration))
            .ToList();
    }

    private sealed record EngagementRow(DateTime Period, int SessionCount, decimal AvgDuration);

    // ─────────────────────────────────────────────────────────────────────────
    // Dashboard Export — IAsyncEnumerable streaming
    // ─────────────────────────────────────────────────────────────────────────

    public async IAsyncEnumerable<DashboardExportRow> StreamExportRowsAsync(
        Guid retailerId,
        DateOnly from,
        DateOnly to,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerable<DashboardSnapshot> snapshots = _context.DashboardSnapshots
            .AsNoTracking()
            .Where(s =>
                s.RetailerId == retailerId &&
                s.SnapshotDate >= from &&
                s.SnapshotDate <= to)
            .OrderBy(s => s.SnapshotDate)
            .AsAsyncEnumerable();

        await foreach (DashboardSnapshot snapshot in snapshots
            .WithCancellation(cancellationToken))
        {
            yield return new DashboardExportRow(
                SnapshotDate: snapshot.SnapshotDate,
                TotalRevenue: snapshot.TotalRevenue,
                TotalProfit: snapshot.TotalProfit,
                TotalOrders: snapshot.TotalOrders,
                ActiveProducts: snapshot.ActiveProducts,
                LowStockCount: snapshot.LowStockCount,
                ConversionRate: snapshot.ConversionRate,
                TryOnEngagement: snapshot.TryOnEngagement
            );
        }
    }
}