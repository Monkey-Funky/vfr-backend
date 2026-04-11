using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Inventory.Events;

/// <summary>
/// Handles LowStockWarningEvent raised by AdjustStockCommandHandler
/// after a manual stock adjustment causes CurrentStock &lt;= LowStockThreshold.
///
/// BEHAVIOUR:
///   1. Load the retailer's NotificationPreferences.
///      If not found, skip all notifications (preferences not set = opt-out by default).
///   2. If LowStockAlerts = true → create an in-app Notification.
///   3. If EmailNotifications = true → send a low-stock alert email via IEmailService.
///
/// EMAIL FAILURE ISOLATION:
///   Email sending failure is caught and logged — does NOT roll back the DB transaction.
/// </summary>
public sealed class LowStockWarningEventHandler
    : INotificationHandler<LowStockWarningEvent>
{
    private static readonly TimeSpan DedupWindow = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LowStockWarningEventHandler> _logger;

    public LowStockWarningEventHandler(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IApplicationDbContext context,
        ILogger<LowStockWarningEventHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _context = context;
        _logger = logger;
    }

    public async Task Handle(
        LowStockWarningEvent notification,
        CancellationToken cancellationToken)
    {
        // ── 1. Load notification preferences ────────────────────────────────
        var preferences = await _unitOfWork.Repository<NotificationPreference>()
            .FirstOrDefaultAsync(
                p => p.RetailerId == notification.RetailerId,
                cancellationToken);

        if (preferences is null)
        {
            _logger.LogWarning(
                "LowStockWarningEventHandler: No NotificationPreferences found for " +
                "Retailer {RetailerId}. Skipping all notifications.",
                notification.RetailerId);
            return;
        }

        // ── 2. In-app notification with 24-hour duplicate suppression ────────
        if (preferences.LowStockAlerts)
        {
            DateTime dedupCutoff = DateTime.UtcNow.Subtract(DedupWindow);

            // AnyAsync — never CountAsync
            bool alreadyExists = await _context.Notifications
                .AsNoTracking()
                .AnyAsync(
                    n => n.RetailerId == notification.RetailerId
                      && n.Type == Notification.NotificationType.LowStock
                      && n.ResourceId == notification.ProductId
                      && !n.IsRead
                      && n.CreatedAt >= dedupCutoff,
                    cancellationToken);

            if (alreadyExists)
            {
                _logger.LogDebug(
                    "LowStockWarningEventHandler: Duplicate suppressed for " +
                    "Product {ProductId} within {Window}h dedup window.",
                    notification.ProductId, DedupWindow.TotalHours);
            }
            else
            {
                var stockNotification = Notification.Create(
                    retailerId: notification.RetailerId,
                    type: Notification.NotificationType.LowStock,
                    title: "Low Stock Alert",
                    body: $"Product '{notification.ProductName}' now has only " +
                                $"{notification.CurrentStock} unit(s) remaining " +
                                $"(threshold: {notification.LowStockThreshold}).",
                    resourceId: notification.ProductId);

                await _unitOfWork.Repository<Notification>()
                    .AddAsync(stockNotification, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Low-stock in-app notification created for Product {ProductId} " +
                    "— Stock: {Stock}/{Threshold}",
                    notification.ProductId,
                    notification.CurrentStock,
                    notification.LowStockThreshold);
            }
        }

        // ── 3. Email notification (failure is non-fatal) ─────────────────────
        if (preferences.EmailNotifications)
        {
            try
            {
                var retailer = await _unitOfWork.Repository<RetailerAccount>()
                    .FirstOrDefaultAsync(
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

                await _emailService.SendEmailAsync(
                    to: retailer.Email,
                    subject: $"[VFR] Low Stock Alert — {notification.ProductName}",
                    body:
                        $"<p>Hello {retailer.FullName},</p>" +
                        $"<p>Your product <strong>{notification.ProductName}</strong> " +
                        $"is running low on stock.</p>" +
                        $"<ul>" +
                        $"  <li><strong>Current Stock:</strong> {notification.CurrentStock}</li>" +
                        $"  <li><strong>Low Stock Threshold:</strong> {notification.LowStockThreshold}</li>" +
                        $"</ul>" +
                        $"<p>Please restock at your earliest convenience.</p>" +
                        $"<p>— The VFR Platform Team</p>",
                    ct: cancellationToken);

                _logger.LogInformation(
                    "Low-stock email sent to {Email} for Product {ProductId}.",
                    retailer.Email, notification.ProductId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "LowStockWarningEventHandler: Failed to send low-stock email " +
                    "for Product {ProductId} (Retailer {RetailerId}).",
                    notification.ProductId, notification.RetailerId);
            }
        }
    }
}