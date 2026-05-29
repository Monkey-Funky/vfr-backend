using Application.Interfaces.Persistence;
using Domain.Entities.Subscriptions;
using Domain.Enums.Subscription;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Hourly background job that expires subscriptions whose billing period or trial has ended.
///
/// PROCESSING RULES:
///   1. Active / PendingDowngrade subscriptions where EndDate &lt;= UtcNow → Expire().
///   2. Trial subscriptions where TrialEndsAt &lt;= UtcNow → Expire().
///   3. Each subscription is processed independently — one failure does NOT abort the batch.
///   4. Publishes SubscriptionExpiredEvent for each expired subscription, triggering the
///      existing SubscriptionExpiryNotificationHandler which creates retailer notifications.
///
/// IDEMPOTENCY: Safe to re-run. The WHERE predicate excludes already-expired subscriptions
/// because their Status is no longer Active/PendingDowngrade/Trial.
///
/// SCHEDULE: Every hour at :05 past the hour (e.g. 00:05, 01:05, 02:05, ...).
/// Uses a simple periodic timer instead of CronScheduler since this runs hourly, not daily.
/// </summary>
public sealed class SubscriptionExpiryJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionExpiryJob> _logger;

    public SubscriptionExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Job} hosted service started.", nameof(SubscriptionExpiryJob));

        // Initial delay — wait 30 seconds after startup to let the app fully initialize.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
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
                    "{Job} encountered an unhandled exception. " +
                    "The job will retry on the next interval.",
                    nameof(SubscriptionExpiryJob));
            }

            await Task.Delay(Interval, stoppingToken);
        }

        _logger.LogInformation("{Job} hosted service stopped.", nameof(SubscriptionExpiryJob));
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Job} run started at {UtcNow:O}.",
            nameof(SubscriptionExpiryJob), DateTime.UtcNow);

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        IApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        IPublisher publisher =
            scope.ServiceProvider.GetRequiredService<IPublisher>();

        DateTime utcNow = DateTime.UtcNow;

        // ── Step 1: Expire Active / PendingDowngrade subscriptions past EndDate ──
        List<Subscription> expiredBilling = await context.Subscriptions
            .Where(s =>
                (s.Status == SubscriptionStatus.Active
                 || s.Status == SubscriptionStatus.PendingDowngrade)
                && s.EndDate.HasValue
                && s.EndDate.Value <= utcNow)
            .ToListAsync(cancellationToken);

        // ── Step 2: Expire Trial subscriptions past TrialEndsAt ─────────────────
        List<Subscription> expiredTrials = await context.Subscriptions
            .Where(s =>
                s.Status == SubscriptionStatus.Trial
                && s.TrialEndsAt.HasValue
                && s.TrialEndsAt.Value <= utcNow)
            .ToListAsync(cancellationToken);

        List<Subscription> allExpired = [.. expiredBilling, .. expiredTrials];

        if (allExpired.Count == 0)
        {
            _logger.LogDebug("{Job} completed. No subscriptions to expire.",
                nameof(SubscriptionExpiryJob));
            return;
        }

        int succeeded = 0;
        int failed = 0;

        foreach (Subscription subscription in allExpired)
        {
            try
            {
                subscription.Expire();
                await context.SaveChangesAsync(cancellationToken);

                // Publish domain event — triggers SubscriptionExpiryNotificationHandler.
                await publisher.Publish(
                    new SubscriptionExpiredEvent(
                        RetailerId: subscription.RetailerId,
                        ExpiresAt: subscription.EndDate ?? subscription.TrialEndsAt ?? utcNow,
                        OccurredAt: utcNow),
                    cancellationToken);

                succeeded++;
            }
            catch (Exception ex)
            {
                // Per-subscription isolation — failure does NOT abort the batch.
                _logger.LogError(ex,
                    "{Job}: failed to expire Subscription {SubscriptionId} " +
                    "for Retailer {RetailerId}. Continuing with remaining subscriptions.",
                    nameof(SubscriptionExpiryJob), subscription.Id, subscription.RetailerId);
                failed++;
            }
        }

        _logger.LogInformation(
            "{Job} completed. Expired: {Succeeded}, Failed: {Failed} " +
            "(Billing: {BillingCount}, Trial: {TrialCount}).",
            nameof(SubscriptionExpiryJob), succeeded, failed,
            expiredBilling.Count, expiredTrials.Count);
    }
}
