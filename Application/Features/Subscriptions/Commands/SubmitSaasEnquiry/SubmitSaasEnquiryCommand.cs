namespace Application.Features.Subscriptions.Commands.SubmitSaasEnquiry;

/// <summary>
/// Submits a SaaS/White-Label enquiry on behalf of the authenticated retailer.
/// Raises a SaasEnquirySubmittedDomainEvent which triggers an admin notification.
/// </summary>
public sealed record SubmitSaasEnquiryCommand : IRequest<Result<Guid>>;