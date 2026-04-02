namespace Domain.Events;

/// <summary>
/// Raised when a retailer submits a SaaS/White-Label enquiry.
/// Triggers an admin notification via the INotificationHandler in the Application layer.
/// </summary>
public sealed record SaasEnquirySubmittedDomainEvent(
    Guid EnquiryId,
    Guid RetailerId,
    DateTime OccurredAt
) : IDomainEvent;