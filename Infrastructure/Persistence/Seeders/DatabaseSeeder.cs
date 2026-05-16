// src/Infrastructure/Persistence/Seeders/DatabaseSeeder.cs
namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Orchestrates all database seeders in the correct order.
///
/// USAGE — in Program.cs, after var app = builder.Build():
///
///     await DatabaseSeeder.SeedAsync(app.Services);
///
/// DESIGN RULES:
///   • Each seeder is resolved from a fresh DI scope so it gets its own
///     DbContext instance — safe for async, no shared-state bugs.
///   • Seeders run sequentially in the order listed below.
///     If a seeder depends on another seeder's data, add it AFTER.
///   • A seeder failure logs the error and re-throws, aborting startup.
///     This is intentional: running without seed data produces silent failures
///     (e.g., "no plans found") that are harder to diagnose than a crash.
///   • Adding a new seeder: implement ISeeder, register it as Scoped in
///     DependencyInjection.cs, and add one line below.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    /// Runs all registered seeders. Call this once during application startup,
    /// after migrations have been applied.
    /// </summary>
    /// <param name="services">The root service provider from app.Services.</param>
    /// <param name="cancellationToken">Propagated from the host's cancellation token.</param>
    public static async Task SeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DatabaseSeeder));

        logger.LogInformation("DatabaseSeeder: starting seed pass...");

        // ── Seeder execution order ─────────────────────────────────────────────
        //
        // Keep SubscriptionPlanSeeder first — other seeders may depend on plan IDs.

        await RunSeederAsync<SubscriptionPlanSeeder>(scope, logger, cancellationToken);
        await RunSeederAsync<ExcelDataSeeder>(scope, logger, cancellationToken);

        // Transactional / analytics data — depends on ExcelDataSeeder products being present.
        await RunSeederAsync<TransactionalDataSeeder>(scope, logger, cancellationToken);

        // Future seeders go here:
        // await RunSeederAsync<AdminAccountSeeder>(scope, logger, cancellationToken);

        logger.LogInformation("DatabaseSeeder: all seeders completed successfully.");
    }

    // ── Private Helper ─────────────────────────────────────────────────────────

    private static async Task RunSeederAsync<TSeeder>(
        AsyncServiceScope scope,
        ILogger logger,
        CancellationToken cancellationToken)
        where TSeeder : class, ISeeder   // ← compile-time guarantee; no dynamic dispatch
    {
        var seeder = scope.ServiceProvider.GetRequiredService<TSeeder>();

        logger.LogDebug("DatabaseSeeder: running {SeederName}...", typeof(TSeeder).Name);

        try
        {
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Log with full context before re-throwing so the startup failure is obvious.
            logger.LogCritical(
                ex,
                "DatabaseSeeder: {SeederName} failed — application startup aborted.",
                typeof(TSeeder).Name);
            throw;
        }
    }
}