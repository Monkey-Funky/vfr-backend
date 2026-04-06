using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;


namespace Application.Features.Inventory.Events;

/// <summary>
/// Handles <see cref="LowStockWarningEvent"/> raised by AdjustStockCommandHandler
/// after a manual stock adjustment causes CurrentStock &lt;= LowStockThreshold.
///
/// BEHAVIOUR:
///   1. Load the retailer's NotificationPreferences.
///      If not found, skip all notifications (preferences not set = opt-out by default).
///   2. If NotificationPreferences.LowStockAlerts = true →
///         Create a Notification(Type = LowStock, ...) for the retailer.
///   3. If NotificationPreferences.EmailNotifications = true →
///         Send a low-stock alert email via IEmailService.
///
/// TRANSACTION:
///   This handler is published from inside AdjustStockCommandHandler's
///   ExecuteInTransactionAsync block. The Notification.AddAsync() call participates
///   in the same database transaction. If this handler throws, the entire transaction
///   rolls back — including the stock adjustment itself.
///
/// EMAIL FAILURE ISOLATION:
///   Email sending is fire-and-forget with respect to the DB transaction.
///   If the email service throws, the exception is caught and logged — the stock
///   adjustment and in-app notification are NOT rolled back due to an email failure.
///   This mirrors the established pattern from CommissionDeductionHandler (P-030).
/// </summary>
public sealed class LowStockWarningEventHandler
    : INotificationHandler<LowStockWarningEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<LowStockWarningEventHandler> _logger;

    public LowStockWarningEventHandler(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<LowStockWarningEventHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Handle(
        LowStockWarningEvent notification,
        CancellationToken cancellationToken)
    {
        // ── 1. Load notification preferences ────────────────────────────────────────
        var preferencesRepo = _unitOfWork.Repository<NotificationPreference>();

        var preferences = await preferencesRepo.FirstOrDefaultAsync(
            p => p.RetailerId == notification.RetailerId,
            cancellationToken);

        if (preferences is null)
        {
            _logger.LogWarning(
                "LowStockWarningEventHandler: No NotificationPreferences found for " +
                "Retailer {RetailerId}. Skipping all low-stock notifications.",
                notification.RetailerId);
            return;
        }

        // ── 2. In-app notification ───────────────────────────────────────────────────
        if (preferences.LowStockAlerts)
        {
            var notificationRepo = _unitOfWork.Repository<Notification>();

            // FIX 1: notification.LowStockThreshold (was notification.Threshold — Error 1)
            var stockNotification = Notification.Create(
                retailerId: notification.RetailerId,
                type: Notification.NotificationType.LowStock,
                title: "Low Stock Alert",
                body: $"Product '{notification.ProductName}' has crossed the low-stock " +
                            $"threshold. Current stock: {notification.CurrentStock} unit(s) " +
                            $"(threshold: {notification.LowStockThreshold}).",
                resourceId: notification.ProductId);

            await notificationRepo.AddAsync(stockNotification, cancellationToken);

            // Save the in-app notification within the same open transaction scope.
            // This is the second SaveChangesAsync call inside ExecuteInTransactionAsync —
            // EF Core tracks only the new Notification entity here; the inventory changes
            // were already committed in the first SaveChangesAsync inside the command handler.
            // However, since we are still inside the same transaction boundary, this second
            // save also participates atomically — if it throws, the outer transaction rolls back.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Low-stock in-app notification created for Product {ProductId} " +
                "— Stock: {Stock} / Threshold: {Threshold} (Retailer {RetailerId})",
                notification.ProductId,
                notification.CurrentStock,
                notification.LowStockThreshold,  // FIX 1: LowStockThreshold not Threshold
                notification.RetailerId);
        }

        // ── 3. Email notification — fire-and-forget; failure is non-fatal ────────────
        // The DB transaction is committed before reaching this block when this handler
        // is invoked from the outermost ExecuteInTransactionAsync. Email failure here
        // will NOT roll back the stock adjustment or the in-app notification.
        if (preferences.EmailNotifications)
        {
            try
            {
                // Load the retailer's email and display name for the email body.
                // Use AsNoTracking-equivalent via repository read.
                var retailerRepo = _unitOfWork.Repository<RetailerAccount>();

                var retailer = await retailerRepo.FirstOrDefaultAsync(
                    r => r.Id == notification.RetailerId && !r.IsDeleted,
                    cancellationToken);

                if (retailer is null)
                {
                    _logger.LogWarning(
                        "LowStockWarningEventHandler: Retailer {RetailerId} not found. " +
                        "Skipping email notification.",
                        notification.RetailerId);
                    return;
                }

                // FIX 2 (Error 2):
                //   Wrong (original): _emailService.SendAsync(toEmail:, subject:, body:, cancellationToken:)
                //   Correct:          _emailService.SendEmailAsync(to:, subject:, body:, ct:)
                //   Source:           IEmailService interface in 11-Auth-DomainApp.md §3
                //     Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
                await _emailService.SendEmailAsync(
                    to: retailer.Email,
                    subject: $"[VFR] Low Stock Alert — {notification.ProductName}",
                    body:
                        $"<p>Hello {retailer.FullName},</p>" +
                        $"<p>Your product <strong>{notification.ProductName}</strong> " +
                        $"has crossed the low-stock threshold.</p>" +
                        $"<ul>" +
                        $"  <li><strong>Current Stock:</strong> {notification.CurrentStock} unit(s)</li>" +
                        $"  <li><strong>Low Stock Threshold:</strong> {notification.LowStockThreshold}</li>" +
                        $"</ul>" +
                        $"<p>Please restock at your earliest convenience to avoid stockouts.</p>" +
                        $"<p>— The VFR Platform Team</p>",
                    ct: cancellationToken);

                _logger.LogInformation(
                    "Low-stock alert email sent to {Email} for Product {ProductId} " +
                    "(Retailer {RetailerId}).",
                    retailer.Email, notification.ProductId, notification.RetailerId);
            }
            catch (Exception ex)
            {
                // Email failure is isolated from the DB transaction.
                // Log and continue — do NOT rethrow.
                // The stock adjustment and in-app notification are already persisted.
                _logger.LogError(ex,
                    "LowStockWarningEventHandler: Failed to send low-stock email " +
                    "for Product {ProductId} (Retailer {RetailerId}). " +
                    "Stock adjustment and in-app notification are unaffected.",
                    notification.ProductId, notification.RetailerId);
            }
        }
    }
}