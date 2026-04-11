using Application.Interfaces.Persistence;
using Domain.Entities.Notifications;
using Domain.Enums.Orders;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Orders.Events;

/// <summary>
/// Records platform commission when an order is Delivered.
/// Creates an in-app notification confirming the commission deduction.
///
/// FIX: Notification now extends BaseEntity → IUnitOfWork.Repository{Notification}() is valid.
///
/// TRANSACTION: Runs INSIDE the same ExecuteInTransactionAsync block.
/// Failure here rolls back the entire transaction — including the status change
/// and the inventory decrement — providing all-or-nothing atomicity.
///
/// NOTE: Commission calculation uses a flat 5% platform rate. In production,
/// this should be driven by the retailer's subscription plan tier.
/// </summary>
public sealed class CommissionDeductionHandler
    : INotificationHandler<OrderStatusChangedEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CommissionDeductionHandler> _logger;

    public CommissionDeductionHandler(
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        ILogger<CommissionDeductionHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _logger = logger;
    }

    public async Task Handle(
        OrderStatusChangedEvent notification,
        CancellationToken cancellationToken)
    {
        // Commission is recorded only when the order is marked Delivered
        if (notification.NewStatus != OrderStatus.Delivered)
            return;

        // ── Step 1: Read commission rate from retailer's current subscription ─
        // Join Subscriptions → SubscriptionPlans to get the CommissionRate snapshot.
        // IMPORTANT: We read the plan at delivery time so the rate is correct
        // even if the retailer later changes plans.
        var subscriptionInfo = await _context.Subscriptions
            .AsNoTracking()
            .Where(s => s.RetailerId == notification.RetailerId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.PlanId, s.Plan.CommissionRate, s.Plan.Currency })
            .FirstOrDefaultAsync(cancellationToken);

        decimal commissionRate;
        Guid subscriptionPlanId;
        string currency;

        if (subscriptionInfo is null)
        {
            // Fallback: no active subscription found — 0% rate, log warning
            _logger.LogWarning(
                "CommissionDeductionHandler: No active subscription for Retailer {RetailerId}. " +
                "Using 0% commission rate for Order {OrderId}.",
                notification.RetailerId, notification.OrderId);

            commissionRate = 0m;
            subscriptionPlanId = Guid.Empty;      // sentinel — no plan
            currency = "EGP";
        }
        else
        {
            commissionRate = subscriptionInfo.CommissionRate;
            subscriptionPlanId = subscriptionInfo.PlanId;
            currency = subscriptionInfo.Currency ?? "EGP";
        }

        // ── Step 2: Calculate commission ──────────────────────────────────────
        decimal orderTotal = notification.Items.Sum(i => i.Total);
        decimal commission = Math.Round(orderTotal * commissionRate, 2);

        // ── Step 3: Persist CommissionRecord (FIX OR-2) ───────────────────────
        // Only create a record if there's a valid plan — skip if fallback sentinel
        if (subscriptionPlanId != Guid.Empty)
        {
            var commissionRecord = CommissionRecord.Create(
                retailerId: notification.RetailerId,
                orderId: notification.OrderId,
                subscriptionPlanId: subscriptionPlanId,
                commissionRate: commissionRate,
                orderTotal: orderTotal,
                currency: currency,
                deliveredAt: DateTime.UtcNow);

            var commissionRepo = _unitOfWork.Repository<CommissionRecord>();
            await commissionRepo.AddAsync(commissionRecord, cancellationToken);
        }

        // ── Step 4: Create in-app notification for retailer visibility ────────
        var notificationRepo = _unitOfWork.Repository<Notification>();

        var commissionNotification = Notification.Create(
            retailerId: notification.RetailerId,
            type: Notification.NotificationType.OrderStatusChanged,
            title: "Commission Deducted",
            body: $"Order {notification.OrderId} delivered. " +
                        $"Platform commission of {commission:F2} {currency} " +
                        $"({commissionRate * 100:F1}%) has been deducted from your balance.",
            resourceId: notification.OrderId);

        await notificationRepo.AddAsync(commissionNotification, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Commission deducted for Order {OrderId}: {Commission} {Currency} " +
            "({Rate}% of {Total} {Currency}). Plan: {PlanId}",
            notification.OrderId, commission, currency,
            commissionRate * 100, orderTotal, currency, subscriptionPlanId);
    }
}