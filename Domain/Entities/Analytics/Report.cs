
using Domain.Enums.Analytics;

namespace Domain.Entities.Analytics;

/// <summary>
/// Tracks an asynchronous report generation request.
///
/// Lifecycle:
///   POST /reports → creates record with Status=Pending, returns reportId (202).
///   ReportGenerationJob picks up the reportId, computes the PDF, uploads to S3,
///   then updates Status=Ready and sets ReportUrl.
///   GET /reports/{reportId} → polls Status; returns ReportUrl when Ready.
/// </summary>
public sealed class Report
{
    public Guid Id { get; private set; }
    public Guid RetailerId { get; private set; }

    /// <summary>Current lifecycle status of the report generation job.</summary>
    public ReportStatus Status { get; private set; }

    /// <summary>
    /// The date range this report covers.
    /// </summary>
    public DateOnly RangeFrom { get; private set; }
    public DateOnly RangeTo { get; private set; }

    /// <summary>Pre-signed S3 URL set when Status=Ready. Null until generation completes.</summary>
    public string? ReportUrl { get; private set; }

    /// <summary>Human-readable error message set when Status=Failed.</summary>
    public string? FailureReason { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private Report() { } // EF Core

    public Report(Guid retailerId, DateOnly rangeFrom, DateOnly rangeTo)
    {
        Id = Guid.NewGuid();
        RetailerId = retailerId;
        RangeFrom = rangeFrom;
        RangeTo = rangeTo;
        Status = ReportStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Called by ReportGenerationJob when the PDF is ready and uploaded.</summary>
    public void MarkReady(string reportUrl)
    {
        Status = ReportStatus.Ready;
        ReportUrl = reportUrl;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>Called by ReportGenerationJob when generation fails.</summary>
    public void MarkFailed(string reason)
    {
        Status = ReportStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>Called by ReportGenerationJob immediately when it picks up the job.</summary>
    public void MarkProcessing()
    {
        Status = ReportStatus.Processing;
    }
}