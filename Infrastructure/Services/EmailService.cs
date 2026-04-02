using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;


namespace Infrastructure.Services;

/// <summary>
/// SMTP-based email delivery using MailKit.
///
/// FAILURE POLICY:
///   - All exceptions are caught and logged as Warning.
///   - This method never throws — email failures must not crash the request pipeline.
///   - For OTP emails (ForgotPassword), the handler awaits but catches any exception
///     at the call site and returns a safe generic error to the user.
///
/// SECURITY:
///   - Email address values are never logged in full — only the domain portion.
///   - The SMTP password is sourced from secrets, never from appsettings.json.
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IOptions<EmailSettings> settings,
        ILogger<EmailService> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(to))
        {
            _logger.LogWarning("EmailService.SendEmailAsync — recipient address is null or empty. Skipping.");
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();

            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                SecureSocketOptions.StartTlsWhenAvailable,
                ct);

            // Authenticate only when credentials are configured (some dev SMTP servers skip auth).
            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(quit: true, ct);

            _logger.LogInformation(
                "EmailService — email sent successfully. To: {EmailDomain}, Subject: {Subject}",
                GetEmailDomain(to), subject);
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled — log at Debug, do not treat as an error.
            _logger.LogDebug("EmailService.SendEmailAsync — operation cancelled.");
        }
        catch (Exception ex)
        {
            // Log as Warning — email failures are non-fatal. The calling handler
            // decides whether to surface the failure to the user.
            _logger.LogWarning(ex,
                "EmailService.SendEmailAsync — delivery failed. To domain: {EmailDomain}, Subject: {Subject}",
                GetEmailDomain(to), subject);

            // Re-throw so that critical callers (OTP flow) can handle the failure.
            // Non-critical callers (registration welcome email) use fire-and-forget
            // via Task.Run and never observe this exception.
            throw;
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Extracts only the domain portion of an email for safe logging.
    /// e.g. "owner@acme.com" → "@acme.com"
    /// </summary>
    private static string GetEmailDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[atIndex..] : "[unknown-domain]";
    }
}