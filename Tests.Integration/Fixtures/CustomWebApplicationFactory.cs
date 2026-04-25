using Application.Interfaces.External;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using DotNet.Testcontainers.Builders;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Respawn;
using StackExchange.Redis;
using Testcontainers.PostgreSql;

namespace Tests.Integration.Fixtures;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that:
///   1. Spins up a real PostgreSQL via Testcontainers
///   2. Replaces Redis with in-memory distributed cache
///   3. Stubs external services (S3, Email, Stripe, Google, etc.)
///   4. Removes background hosted services
///   5. Installs <see cref="TestAuthHandler"/> for JWT bypass
///   6. Exposes <see cref="ResetDatabaseAsync"/> for per-test cleanup via Respawn
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("vfr_test")
        .WithUsername("test")
        .WithPassword("test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private Respawner? _respawner;

    // ── IAsyncLifetime ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Force the host to start so migrations run (Program.cs calls MigrateAsync)
        _ = Server;

        // Build the Respawner after migrations have been applied
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // Preserve EF migration history and seeded reference data
            TablesToIgnore = ["__EFMigrationsHistory", "subscription_plans"],
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Resets all table data (except migrations + seed data) between tests.
    /// Call at the start of each test or in a fixture setup.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null) return;

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }

    // ── WebApplicationFactory overrides ───────────────────────────────────────

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Provide configuration values needed by services under test
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("EncryptionSettings:Key", "integration-test-encryption-key-that-is-long-enough");
        builder.UseSetting("JwtSettings:StepTokenSecret", "integration-test-step-secret-at-least-32-chars!!");
        builder.UseSetting("JwtSettings:Issuer", "vfr-test");
        builder.UseSetting("JwtSettings:Audience", "vfr-test");
        builder.UseSetting("JwtSettings:AccessTokenExpiryMinutes", "60");

        builder.ConfigureTestServices(services =>
        {
            // ── 1. Replace PostgreSQL DbContext with Testcontainers connection ──
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options
                    .UseNpgsql(_postgres.GetConnectionString())
                    .UseSnakeCaseNamingConvention());

            // ── 2. Replace Redis with in-memory distributed cache ───────────────
            services.RemoveAll<IConnectionMultiplexer>();
            // Remove the Redis-based IDistributedCache registrations
            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();

            // ── 3. Stub external services ───────────────────────────────────────
            services.RemoveAll<Amazon.S3.IAmazonS3>();
            services.AddSingleton<Amazon.S3.IAmazonS3>(
                new Moq.Mock<Amazon.S3.IAmazonS3>().Object);

            ReplaceWithStub<IEmailService>(services);
            ReplaceWithStub<IFileStorageService>(services);
            ReplaceWithStub<IS3StorageService>(services);
            ReplaceWithStub<IGoogleAuthService>(services);
            ReplaceWithStub<IPaymentGatewayService>(services);
            ReplaceWithStub<IVirtualTryOnService>(services);
            ReplaceWithStub<ISizeRecommendationService>(services);
            ReplaceWithStub<INotificationHub>(services);

            // ── 4. Remove background hosted services ────────────────────────────
            RemoveHostedService<ReportGenerationJob>(services);

            // ── 5. Replace authentication with TestAuthHandler ──────────────────
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="HttpClient"/> pre-configured with default Retailer auth headers.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        string? userId = null,
        string role = "Retailer",
        string email = "test@retailer.com")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId ?? TestAuthHandler.DefaultRetailerId);
        client.DefaultRequestHeaders.Add("X-Test-Role", role);
        client.DefaultRequestHeaders.Add("X-Test-Email", email);
        return client;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that simulates an unauthenticated caller.
    /// </summary>
    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return client;
    }

    /// <summary>
    /// Provides a scoped <see cref="ApplicationDbContext"/> for test-side data seeding/assertions.
    /// </summary>
    public async Task ExecuteDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(db);
    }

    private static void ReplaceWithStub<TService>(IServiceCollection services)
        where TService : class
    {
        services.RemoveAll<TService>();
        services.AddScoped(_ => new Moq.Mock<TService>().Object);
    }

    private static void RemoveHostedService<TService>(IServiceCollection services)
        where TService : class, IHostedService
    {
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TService));

        if (descriptor is not null)
            services.Remove(descriptor);
    }
}
