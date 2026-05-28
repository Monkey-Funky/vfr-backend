using Application.Interfaces.External;
using Application.Interfaces.Services;
using Moq;
using Application.Interfaces.Services.Customer;
using Domain.Enums.Customer;
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

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // FIX: The field initializer called Build() which invokes AbstractBuilder.Validate(),
    // which checks Docker connectivity at class construction time — before InitializeAsync
    // ever runs. This caused an ArgumentException ("Docker is either not running or
    // misconfigured") to be thrown from the constructor, instantly failing every test in
    // the collection. Build() + StartAsync() belong in InitializeAsync, which is the
    // correct lifecycle method for async setup work in IAsyncLifetime.
    private PostgreSqlContainer _postgres = null!;

    private Respawner? _respawner;

    public Mock<IVirtualTryOnService> VirtualTryOnServiceMock { get; } = new Mock<IVirtualTryOnService>();
    public Mock<ISizeRecommendationService> SizeRecommendationServiceMock { get; } = new Mock<ISizeRecommendationService>();

    public async Task InitializeAsync()
    {
        // Build and start the PostgreSQL container here, not in the field initializer.
        // Build() validates Docker connectivity; keeping it in InitializeAsync means the
        // validation happens in the async setup phase, not in the constructor.
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("vfr_test")
            .WithUsername("test")
            .WithPassword("test")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await _postgres.StartAsync();

        _ = Server;

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = ["__EFMigrationsHistory", "subscription_plans"],
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null) return;

        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);

        VirtualTryOnServiceMock.Reset();
        SizeRecommendationServiceMock.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("EncryptionSettings:Key", "integration-test-encryption-key-that-is-long-enough");
        builder.UseSetting("JwtSettings:StepTokenSecret", "integration-test-step-secret-at-least-32-chars!!");
        builder.UseSetting("JwtSettings:Issuer", "vfr-test");
        builder.UseSetting("JwtSettings:Audience", "vfr-test");
        builder.UseSetting("JwtSettings:AccessTokenExpiryMinutes", "60");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options
                    .UseNpgsql(_postgres.GetConnectionString())
                    .UseSnakeCaseNamingConvention());

            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<Amazon.S3.IAmazonS3>();
            services.AddSingleton<Amazon.S3.IAmazonS3>(
                new Moq.Mock<Amazon.S3.IAmazonS3>().Object);

            ReplaceWithStub<IEmailService>(services);

            // IMPORTANT: Use the explicit generic overload AddSingleton<TService>(instance)
            // so the mock is registered under the IFileStorageService interface type.
            // Without the type parameter, AddSingleton(object) infers the Moq proxy
            // runtime type, which means IFileStorageService cannot be resolved from DI.
            var fileStorageMock = new Mock<IFileStorageService>();
            fileStorageMock
                .Setup(x => x.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://cdn.vfr.com/test-file.jpg");
            services.RemoveAll<IFileStorageService>();
            services.AddSingleton<IFileStorageService>(fileStorageMock.Object);

            // Same explicit-interface pattern for IS3StorageService.
            var s3StorageMock = new Mock<IS3StorageService>();
            s3StorageMock
                .Setup(x => x.UploadReportAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://s3.vfr.com/test-file.jpg");
            services.RemoveAll<IS3StorageService>();
            services.AddSingleton<IS3StorageService>(s3StorageMock.Object);

            ReplaceWithStub<ICacheService>(services);
            ReplaceWithStub<IGoogleAuthService>(services);
            ReplaceWithStub<IPaymentGatewayService>(services);
            ReplaceWithStub<INotificationHub>(services);

            services.RemoveAll<IVirtualTryOnService>();
            services.AddScoped(_ => VirtualTryOnServiceMock.Object);

            services.RemoveAll<ISizeRecommendationService>();
            services.AddScoped(_ => SizeRecommendationServiceMock.Object);

            RemoveHostedService<ReportGenerationJob>(services);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }

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

    public HttpClient CreateAnonymousClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");
        return client;
    }

    public async Task ExecuteDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(db);
    }

    public async Task ExecuteInScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider);
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