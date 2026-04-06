namespace Application.Interfaces.Services;


/// <summary>
/// Abstraction over the email delivery provider (SendGrid / SMTP / etc.).
/// The implementation lives in Infrastructure.Services and is registered in
/// Infrastructure/DependencyInjection.cs.
///
/// IMPORTANT: Handlers should use fire-and-forget for non-critical emails
/// (e.g. registration confirmation) by calling this service inside a
/// Task.Run(...) block and NOT awaiting it.
/// Critical transactional emails (OTP codes) must be awaited.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a transactional email.
    /// </summary>
    /// <param name="to">Recipient email address (validated before calling).</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="body">HTML or plain-text body content.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendEmailAsync(
        string to,
        string subject,
        string body,
        CancellationToken ct = default);
}