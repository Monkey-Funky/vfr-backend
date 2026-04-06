using Application.Interfaces.Persistence;
using Domain.Enums.Offer;
using Microsoft.Extensions.Hosting;


namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Nightly job that expires all active offers whose EndDate has passed.
/// Runs daily at 02:00 UTC.
///
/// IDEMPOTENCY: Safe to re-run. The WHERE predicate
///   (EndDate &lt; today AND Status = Active)
/// ensures already-expired offers are not selected on subsequent runs.
///
/// DOMAIN CONTRACT: Calls Offer.Deactivate() on each candidate entity —
/// this is the sole mechanism for transitioning an offer to OfferStatus.Expired.
/// </summary>
public sealed class OfferExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OfferExpiryJob> _logger;

    public OfferExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OfferExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Job} hosted service started", nameof(OfferExpiryJob));

        while (!stoppingToken.IsCancellationRequested)
        {
            await CronScheduler.WaitForNextOccurrenceAsync("02:00", stoppingToken);

            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown — exit the loop cleanly.
                throw;
            }
            catch (Exception ex)
            {
                // Log Fatal but do NOT re-throw — host continues running.
                _logger.LogCritical(ex,
                    "{Job} encountered an unhandled exception and skipped this run. " +
                    "Error: {Message}",
                    nameof(OfferExpiryJob), ex.Message);
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{Job} run started at {UtcNow:O}",
            nameof(OfferExpiryJob), DateTime.UtcNow);

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        IApplicationDbContext context =
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Load only Active, non-deleted offers whose EndDate has passed.
        // The partial index idx_offers_end_date_active covers this query efficiently.
        List<Offer> expiredOffers = await context.Offers
            .Where(o =>
                o.EndDate.HasValue &&
                o.EndDate.Value < today &&
                o.Status == OfferStatus.Active)
            .ToListAsync(cancellationToken);

        // Call the domain method on each entity — this is the contract requirement.
        // Deactivate() transitions status to OfferStatus.Expired and sets UpdatedAt.
        foreach (Offer offer in expiredOffers)
        {
            offer.Deactivate();
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{Job} completed. OffersExpired: {Count}",
            nameof(OfferExpiryJob), expiredOffers.Count);
    }
}
