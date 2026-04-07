
namespace Domain.Entities.Analytics;
/// <summary>
/// Nightly KPI snapshot per retailer, computed by DashboardSnapshotJob at 01:00 UTC.
/// One record per (retailer_id, snapshot_date) pair — UPSERT semantics.
/// Not soft-deleted; historical snapshots are retained for trend analytics.
/// Does NOT extend BaseEntity because it has its own audit field (ComputedAt)
/// rather than the standard CreatedAt/UpdatedAt/IsDeleted set.
/// </summary>
public sealed class DashboardSnapshot
{
    public Guid Id { get; private set; }

    /// <summary>Owning retailer. Every query MUST scope by this column.</summary>
    public Guid RetailerId { get; private set; }

    /// <summary>Calendar date this snapshot covers (yesterday's date when computed).</summary>
    public DateOnly SnapshotDate { get; private set; }

    public decimal TotalRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public int TotalOrders { get; set; }
    public int ActiveProducts { get; set; }
    public int LowStockCount { get; set; }
    public decimal ConversionRate { get; set; }
    public int TryOnEngagement { get; set; }

    /// <summary>UTC timestamp when this snapshot record was last computed.</summary>
    public DateTime ComputedAt { get; set; }

    private DashboardSnapshot() { } // EF Core

    public DashboardSnapshot(
        Guid retailerId,
        DateOnly snapshotDate,
        decimal totalRevenue,
        decimal totalProfit,
        int totalOrders,
        int activeProducts,
        int lowStockCount,
        decimal conversionRate,
        int tryOnEngagement)
    {
        Id = Guid.NewGuid();
        RetailerId = retailerId;
        SnapshotDate = snapshotDate;
        TotalRevenue = totalRevenue;
        TotalProfit = totalProfit;
        TotalOrders = totalOrders;
        ActiveProducts = activeProducts;
        LowStockCount = lowStockCount;
        ConversionRate = conversionRate;
        TryOnEngagement = tryOnEngagement;
        ComputedAt = DateTime.UtcNow;
    }
}