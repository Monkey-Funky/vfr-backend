using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Daily background job that handles automatic subscription renewals for retailers
/// who have IsRecurringEnabled = true.
///
/// PROCESSING RULES:
///   1. Finds subscriptions where EndDate is within the next 24 hours AND IsRecurringEnabled = true.
///   2. Loads the retailer's default payment method (IsDefault = true, not deleted, not expired).
///   3. Creates a SubscriptionPayment record and charges Stripe via IPaymentGatewayService.
///   4. On success: extends the subscription EndDate by the plan's billing cycle.
///      - If Status == PendingDowngrade: applies the pending plan change on renewal.
///   5. On failure: logs the error. The subscription will expire via SubscriptionExpiryJob,
///      and the retailer will receive an expiry notification.
///
/// IDEMPOTENCY: Uses PeriodStartDate/PeriodEndDate overlap checks to prevent double-charging.
///
/// SCHEDULE: Daily at 01:00 UTC via CronScheduler.
///
/// FAILURE ISOLATION: Each retailer is processed independently — one failure does NOT
/// abort the batch for other retailers.
/// </summary>
public sealed class RecurringPaymentJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringPaymentJob> _logger;

    public RecurringPaymentJob(
        IServiceScopeFactory scopeFactory,
        ILogger<RecurringPaymentJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Job} hosted service started.", nameof(RecurringPaymentJob));

        while (!stoppingToken.IsCancellationRequested)
        {
            await CronScheduler.WaitForNextOccurrenceAsync("01:00", stoppingToken);

            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex,
                    "{Job} encountered an unhandled exception and skipped this run.",
                    nameof(RecurringPaymentJob));
            }
        }

        _logger.LogInformation("{Job} hosted service stopped.", nameof(RecurringPaymentJob));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Job} run started at {UtcNow:O}.",
            nameof(RecurringPaymentJob), DateTime.UtcNow);

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        IApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        IPaymentGatewayService paymentGateway =
            scope.ServiceProvider.GetRequiredService<IPaymentGatewayService>();

        DateTime utcNow = DateTime.UtcNow;
        DateTime renewalWindow = utcNow.AddHours(24);

        // ── Find subscriptions due for renewal within the next 24 hours ─────────
        List<Subscription> dueForRenewal = await context.Subscriptions
            .Include(s => s.Plan)
            .Where(s =>
                (s.Status == SubscriptionStatus.Active
                 || s.Status == SubscriptionStatus.PendingDowngrade)
                && s.IsRecurringEnabled
                && s.EndDate.HasValue
                && s.EndDate.Value <= renewalWindow
                && s.EndDate.Value > utcNow)   // Not yet expired
            .ToListAsync(cancellationToken);

        if (dueForRenewal.Count == 0)
        {
            _logger.LogDebug("{Job} completed. No subscriptions due for renewal.",
                nameof(RecurringPaymentJob));
            return;
        }

        _logger.LogInformation("{Job}: processing {Count} subscription(s) for renewal.",
            nameof(RecurringPaymentJob), dueForRenewal.Count);

        int succeeded = 0;
        int failed = 0;

        foreach (Subscription subscription in dueForRenewal)
        {
            try
            {
                await ProcessRenewalAsync(
                    context, paymentGateway,
                    subscription, utcNow, cancellationToken);
                succeeded++;
            }
            catch (Exception ex)
            {
                // Per-retailer isolation — failure does NOT abort the batch.
                _logger.LogError(ex,
                    "{Job}: renewal failed for Subscription {SubscriptionId} " +
                    "(Retailer {RetailerId}). " +
                    "The subscription will expire and the retailer will be notified.",
                    nameof(RecurringPaymentJob), subscription.Id, subscription.RetailerId);
                failed++;
            }
        }

        _logger.LogInformation(
            "{Job} completed. Renewed: {Succeeded}, Failed: {Failed}.",
            nameof(RecurringPaymentJob), succeeded, failed);
    }

    private async Task ProcessRenewalAsync(
        IApplicationDbContext context,
        IPaymentGatewayService paymentGateway,
        Subscription subscription,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        SubscriptionPlan plan = subscription.Plan;

        // ── Idempotency: check if we already created a payment for this period ──
        DateTime currentPeriodEnd = subscription.EndDate!.Value;
        bool alreadyCharged = await context.SubscriptionPayments
            .AsNoTracking()
            .AnyAsync(
                p => p.RetailerId == subscription.RetailerId
                  && p.PeriodStartDate == currentPeriodEnd
                  && p.Status == SubscriptionPaymentStatus.Completed,
                cancellationToken);

        if (alreadyCharged)
        {
            _logger.LogDebug(
                "{Job}: Subscription {SubscriptionId} already has a completed payment " +
                "for the next period. Skipping.",
                nameof(RecurringPaymentJob), subscription.Id);
            return;
        }

        // ── Determine the plan to charge ────────────────────────────────────────
        // If PendingDowngrade, load the pending plan instead.
        SubscriptionPlan chargePlan = plan;
        if (subscription.Status == SubscriptionStatus.PendingDowngrade
            && subscription.PendingDowngradePlanId.HasValue)
        {
            SubscriptionPlan? pendingPlan = await context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    p => p.Id == subscription.PendingDowngradePlanId.Value,
                    cancellationToken);

            if (pendingPlan is not null)
                chargePlan = pendingPlan;
        }

        // ── Load the retailer's default payment method ──────────────────────────
        PaymentMethod? paymentMethod = await context.PaymentMethods
            .FirstOrDefaultAsync(
                pm => pm.RetailerId == subscription.RetailerId
                   && pm.IsDefault
                   && !pm.IsDeleted,
                cancellationToken);

        if (paymentMethod is null)
        {
            _logger.LogWarning(
                "{Job}: No default payment method found for Retailer {RetailerId}. " +
                "Subscription {SubscriptionId} will not be renewed automatically.",
                nameof(RecurringPaymentJob), subscription.RetailerId, subscription.Id);
            return;
        }

        if (paymentMethod.IsExpired)
        {
            _logger.LogWarning(
                "{Job}: Default payment method (****{Last4}) for Retailer {RetailerId} " +
                "is expired. Subscription {SubscriptionId} will not be renewed.",
                nameof(RecurringPaymentJob), paymentMethod.CardNumberLast4,
                subscription.RetailerId, subscription.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(paymentMethod.StripePaymentMethodId))
        {
            _logger.LogWarning(
                "{Job}: Default payment method for Retailer {RetailerId} " +
                "has no Stripe token. Subscription {SubscriptionId} will not be renewed.",
                nameof(RecurringPaymentJob), subscription.RetailerId, subscription.Id);
            return;
        }

        // ── Calculate the new billing period ────────────────────────────────────
        DateTime newPeriodStart = currentPeriodEnd;
        DateTime newPeriodEnd = chargePlan.BillingCycle switch
        {
            "Yearly" => newPeriodStart.AddYears(1),
            "Monthly" => newPeriodStart.AddMonths(1),
            _ => newPeriodStart.AddMonths(1)
        };

        // ── Create payment record (Pending) ────────────────────────────────────
        SubscriptionPayment payment = SubscriptionPayment.Create(
            retailerId: subscription.RetailerId,
            subscriptionPlanId: chargePlan.Id,
            paymentMethodId: paymentMethod.Id,
            amount: chargePlan.PriceAmount,
            currency: chargePlan.Currency,
            periodStartDate: newPeriodStart,
            periodEndDate: newPeriodEnd,
            isRecurring: true);

        context.SubscriptionPayments.Add(payment);
        payment.MarkProcessing();
        await context.SaveChangesAsync(cancellationToken);

        // ── Charge Stripe ───────────────────────────────────────────────────────
        PaymentResult result = await paymentGateway.ChargeAsync(
            paymentMethod.StripePaymentMethodId!,
            chargePlan.PriceAmount,
            chargePlan.Currency.ToLowerInvariant(),
            cancellationToken);

        if (!result.Success)
        {
            payment.MarkFailed();
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "{Job}: Stripe charge failed for Subscription {SubscriptionId}. " +
                "Error: {Error}. The subscription will expire naturally.",
                nameof(RecurringPaymentJob), subscription.Id, result.ErrorMessage);
            return;
        }

        // ── Mark payment as completed ───────────────────────────────────────────
        payment.MarkCompleted(result.StripePaymentIntentId!);
        await context.SaveChangesAsync(cancellationToken);

        // ── Extend the subscription ─────────────────────────────────────────────
        // Activate() handles PendingDowngrade → Active with plan switch.
        subscription.Activate(newPeriodEnd);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Job}: Successfully renewed Subscription {SubscriptionId} " +
            "for Retailer {RetailerId}. Plan: {PlanName}, " +
            "New period: {Start:yyyy-MM-dd} → {End:yyyy-MM-dd}.",
            nameof(RecurringPaymentJob), subscription.Id, subscription.RetailerId,
            chargePlan.Name, newPeriodStart, newPeriodEnd);
    }
}
