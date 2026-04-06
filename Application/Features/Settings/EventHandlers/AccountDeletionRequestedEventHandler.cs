using Application.Interfaces.Services;

namespace Application.Features.Settings.EventHandlers;

/// <summary>
/// Handles <see cref="AccountDeletionRequestedEvent"/> by sending a GDPR-compliant
/// deletion confirmation email to the retailer.
///
/// The email informs the retailer:
///   • Their deletion request was received.
///   • Their account and PII will be permanently erased after a 30-day grace period.
///   • They can cancel the deletion by contacting support before the grace period ends.
///
/// This handler is fire-and-forget safe — a failure here (e.g. SMTP outage) does NOT
/// roll back the account status change. The status transition already committed.
/// If the email fails, it is logged at Warning level; the deletion process continues.
/// </summary>
public sealed class AccountDeletionRequestedEventHandler
    : INotificationHandler<AccountDeletionRequestedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountDeletionRequestedEventHandler> _logger;

    public AccountDeletionRequestedEventHandler(
        IEmailService emailService,
        ILogger<AccountDeletionRequestedEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(
        AccountDeletionRequestedEvent notification,
        CancellationToken cancellationToken)
    {
        string subject = "Account Deletion Request Received — VFR Platform";

        string body = $"""
            <h2>Account Deletion Request Confirmed</h2>
            <p>We have received your account deletion request, submitted on
               {notification.OccurredAt:MMMM dd, yyyy} at {notification.OccurredAt:HH:mm} UTC.</p>
            <p>Your account and all associated personal data will be permanently and
               irreversibly deleted after a <strong>30-day grace period</strong>.</p>
            <p>If you wish to cancel this deletion request before the grace period expires,
               please contact our support team immediately.</p>
            <p>Thank you for using the VFR Platform.</p>
            """;

        try
        {
            await _emailService.SendEmailAsync(
                to: notification.RetailerEmail,
                subject: subject,
                body: body,
                ct: cancellationToken);

            _logger.LogInformation(
                "AccountDeletionRequestedEventHandler — confirmation email sent. " +
                "RetailerId: {RetailerId}", notification.RetailerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AccountDeletionRequestedEventHandler — failed to send confirmation email. " +
                "RetailerId: {RetailerId} | Error: {Message}",
                notification.RetailerId, ex.Message);
        }
    }
}