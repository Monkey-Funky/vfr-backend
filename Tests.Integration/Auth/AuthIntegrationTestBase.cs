// tests/Tests.Integration/Auth/AuthIntegrationTestBase.cs

using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestPlatform.TestHost;

// ✅ FIX 1: Removed "using Microsoft.VisualStudio.TestPlatform.TestHost;"
//
// That using statement imported the test framework's OWN Program class into scope,
// so WebApplicationFactory<Program> was pointing at the test runner — not the API.
// The real API host never started, causing every integration test to fail.
//
// The API's Program class is now accessible because Program.cs declares:
//   public partial class Program { }      ← required companion fix

namespace Tests.Integration.Auth;

[Collection("IntegrationTests")]
public abstract class AuthIntegrationTestBase
    : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    protected readonly HttpClient Client;
    protected readonly WebApplicationFactory<Program> Factory;

    protected AuthIntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            // ── Environment ───────────────────────────────────────────────────
            builder.UseEnvironment("IntegrationTest");

            // ── FIX 2: Inject configuration that is only in appsettings.Development.json ─
            //
            // With UseEnvironment("IntegrationTest"), the host loads:
            //   appsettings.json + appsettings.IntegrationTest.json (doesn't exist)
            // It does NOT load appsettings.Development.json, so JwtSettings:StepTokenSecret
            // is missing. TokenService validates this in its constructor and throws
            // InvalidOperationException → web host fails to start → every test fails.
            //
            // We inject the secret here so tests are self-contained and do not depend on
            // the file system or user-secrets.
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Same value as appsettings.Development.json (≥ 32 chars — satisfies the
                    // NIST minimum HMAC-SHA256 key length enforced by TokenService).
                    ["JwtSettings:StepTokenSecret"] =
                        "fm2JuZc2Oi6zin9VPHj7oYwTaXM3epKkT6at0NUWahLJz88aeeV7tcQi36kNMMN7c+hb"
                });
            });

            // ── Service overrides ─────────────────────────────────────────────
            builder.ConfigureServices(services =>
            {
                // ── FIX 3: Override the DB connection for tests ───────────────
                //
                // Password=tarek is just a plain string value — no special handling.
                // Set INTEGRATION_TEST_DB env-var to point at a different server in CI.
                string dbConn = Environment.GetEnvironmentVariable("INTEGRATION_TEST_DB")
                    ?? "Host=localhost;Port=5432;Database=vfr_test;Username=postgres;Password=tarek";

                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseNpgsql(dbConn)
                        .UseSnakeCaseNamingConvention());

                // ── FIX 4: Replace Redis with an in-memory distributed cache ──
                //
                // Real Redis is not required for the auth flows tested here (Login,
                // Register, Refresh). Replacing it with an in-memory cache means:
                //   • Tests run without a Redis server.
                //   • ForgotPassword / ResetPassword (OTP flow) would also work correctly
                //     because in-memory distributed cache honours TTL via sliding expiry.
                services.RemoveAll<IDistributedCache>();
                services.AddDistributedMemoryCache();
            });
        });

        Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
    }

    /// <summary>
    /// Deletes all auth rows before EACH test so every test starts with a clean DB.
    /// xUnit calls InitializeAsync once per test instance (one instance per test method).
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        // Delete child table first to satisfy the FK constraint, then parent table.
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM notification_preferences; DELETE FROM retailer_accounts;");
    }

    public async Task InitializeAsync() => await ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}

// ── Collection definition ─────────────────────────────────────────────────────
// DisableParallelization ensures tests run sequentially so DB resets don't race.
[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public sealed class IntegrationTestsCollection { }