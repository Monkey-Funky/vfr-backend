
namespace Domain.Entities.Analytics;

/// <summary>
/// Daily aggregate of virtual fitting room engagement metrics per retailer.
/// Computed by DashboardSnapshotJob or a dedicated engagement aggregation step.
/// One record per (retailer_id, metric_date).
/// </summary>
public sealed class VfrEngagementMetric
{
    public Guid Id { get; private set; }
    public Guid RetailerId { get; private set; }
    public DateOnly MetricDate { get; private set; }
    public int TotalTryOns { get; set; }
    public int UniqueCustomers { get; set; }
    public decimal AvgSessionSeconds { get; set; }
    public decimal ConversionRate { get; set; }

    /// <summary>FK to the product with the most try-ons on this date. Nullable.</summary>
    public Guid? TopProductId { get; set; }

    public DateTime RecordedAt { get; set; }

    private VfrEngagementMetric() { } // EF Core

    public VfrEngagementMetric(Guid retailerId, DateOnly metricDate)
    {
        Id = Guid.NewGuid();
        RetailerId = metricDate == default ? throw new ArgumentException("metricDate required") : retailerId;
        MetricDate = metricDate;
        RecordedAt = DateTime.UtcNow;
    }
}