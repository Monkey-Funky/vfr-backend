using Domain.Common;
using Domain.Enums;

namespace Domain.Entities.Subscriptions;

/// <summary>
/// Records a retailer's interest in the SaaS/White-Label plan.
/// Created via SubmitSaasEnquiryCommand; managed by admin dashboard.
///
/// Fields match B.20 of 02-DatabaseSchema.md.
/// </summary>
public sealed class SaasEnquiry : BaseEntity
{
    // ── Properties ────────────────────────────────────────────────────────────

    public Guid RetailerId { get; private set; }
    public SaasEnquiryStatus Status { get; private set; }

    /// <summary>Internal admin notes — never visible to the retailer.</summary>
    public string? Notes { get; private set; }

    // ── Private Constructor (EF Core) ─────────────────────────────────────────

    private SaasEnquiry() { }

    // ── Factory Method ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new SaaS enquiry. Status defaults to Pending.
    /// </summary>
    public static SaasEnquiry Create(Guid retailerId)
    {
        return new SaasEnquiry
        {
            RetailerId = retailerId,
            Status = SaasEnquiryStatus.Pending
        };
    }

    // ── Mutation Methods ──────────────────────────────────────────────────────

    /// <summary>Admin: moves the enquiry into the InProgress state.</summary>
    public void StartProcessing(string? notes = null)
    {
        Status = SaasEnquiryStatus.InProgress;
        Notes = notes;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>Admin: closes the enquiry.</summary>
    public void Close(string? notes = null)
    {
        Status = SaasEnquiryStatus.Closed;
        Notes = notes;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }
}