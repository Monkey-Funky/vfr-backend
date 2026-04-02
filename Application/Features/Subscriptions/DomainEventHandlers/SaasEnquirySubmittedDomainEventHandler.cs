using Domain.Events;
using Microsoft.Extensions.Logging;

namespace Application.Features.Subscriptions.DomainEventHandlers;

/// <summary>
/// Handles the SaasEnquirySubmittedDomainEvent.
/// Responsibility: log the enquiry and trigger an admin notification.
///
/// The actual email/notification dispatch is delegated to a separate
/// infrastructure service (IAdminNotificationService or similar) to be
/// implemented in the Infrastructure phase. For now, the handler logs
/// the event and provides the extension point.
/// </summary>
public sealed class SaasEnquirySubmittedDomainEventHandler
    : INotificationHandler<SaasEnquirySubmittedDomainEvent>
{
    private readonly ILogger<SaasEnquirySubmittedDomainEventHandler> _logger;

    public SaasEnquirySubmittedDomainEventHandler(
        ILogger<SaasEnquirySubmittedDomainEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(
        SaasEnquirySubmittedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "SaaS enquiry submitted — EnquiryId: {EnquiryId} | RetailerId: {RetailerId} | At: {OccurredAt:u}",
            notification.EnquiryId,
            notification.RetailerId,
            notification.OccurredAt);

        // TODO (Infrastructure Phase): Inject IAdminNotificationService and send email
        // to admin@vfr-platform.com with the enquiry details.
        // Example:
        //   await _adminNotificationService.NotifyNewSaasEnquiryAsync(
        //       notification.EnquiryId, notification.RetailerId, cancellationToken);

        return Task.CompletedTask;
    }
}