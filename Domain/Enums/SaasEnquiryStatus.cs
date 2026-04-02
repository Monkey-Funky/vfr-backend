namespace Domain.Enums;

/// <summary>
/// Lifecycle state of a SaaS / White-Label enquiry.
/// Values match the DB CHECK constraint on saas_enquiries.status.
/// </summary>
public enum SaasEnquiryStatus
{
    Pending = 0,
    InProgress = 1,
    Closed = 2
}