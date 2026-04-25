using Amazon;
using Amazon.S3;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Infrastructure.BackgroundJobs;
using Infrastructure.Hubs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence.Seeders;
using Infrastructure.Services.Auth;
using Infrastructure.Services.Communication;
using Infrastructure.Services.Dashboard;
using Infrastructure.Services.Payment;
using Infrastructure.Services.Security;
using Infrastructure.Services.Storage;
using Infrastructure.Services.Subscription;
using Infrastructure.Services.System;
using Infrastructure.Settings;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StackExchange.Redis;

namespace Infrastructure;

/// <summary>
/// Infrastructure layer DI registration.
/// Called from Program.cs: builder.Services.AddInfrastructure(builder.Configuration).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. EF Core + PostgreSQL ───────────────────────────────────────────
        //
        // UseSnakeCaseNamingConvention() is chained here on DbContextOptionsBuilder.
        // This is the ONLY correct location — it must NOT be called on ModelBuilder
        // inside OnModelCreating (that causes a compile error).
        // Requires NuGet: EFCore.NamingConventions
        services.AddDbContext<ApplicationDbContext>(options =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsql => npgsql
                        .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                        .EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null))
                .UseSnakeCaseNamingConvention());   // ← correct: on DbContextOptionsBuilder

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // ── 2. Options bindings ───────────────────────────────────────────────
        //
        // Using the lambda bind form (options => section.Bind(options)) is always
        // safe regardless of which Configure<T> overload the compiler resolves.
        // The IConfiguration overload requires Microsoft.Extensions.Options.ConfigurationExtensions;
        // the lambda form never has that dependency.
        services.Configure<JwtSettings>(options =>
    configuration.GetSection("JwtSettings").Bind(options));

        services.Configure<EmailSettings>(options =>
            configuration.GetSection("Email").Bind(options));

        services.Configure<S3Settings>(options =>
            configuration.GetSection("S3").Bind(options));

        services.Configure<GoogleSettings>(options =>
            configuration.GetSection("Google").Bind(options));

        // ── 3. Polly resilience pipelines ─────────────────────────────────────

        services.AddResiliencePipeline("s3", builder =>
        {
            builder
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(15),
                });
        });

        services.AddResiliencePipeline("email", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Linear,
            });
        });

        // ── 4. AWS S3 client (Singleton — IAmazonS3 is thread-safe) ──────────
        services.AddSingleton<IAmazonS3>(_ =>
        {
            var region = configuration["S3:Region"] ?? "us-east-1";
            return new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
        });

        // ── 5. Application services ───────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();

        // ── 6. Repository & Unit of Work ──────────────────────────────────────
        // These were incorrectly commented out — they are required by all command handlers.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // ── 7. Redis / Distributed Cache ──────────────────────────────────────
        //var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";

        //services.AddSingleton<IConnectionMultiplexer>(_ =>
        //    ConnectionMultiplexer.Connect(redisConnectionString + ",abortConnect=false"));

        //services.AddStackExchangeRedisCache(options =>
        //{
        //    options.Configuration = redisConnectionString + ",abortConnect=false";
        //    options.InstanceName = "vfr:";
        //});

        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";

        // Don't append abortConnect if it's already in the connection string
        var redisConfigString = redisConnectionString.Contains("abortConnect", StringComparison.OrdinalIgnoreCase)
            ? redisConnectionString
            : redisConnectionString + ",abortConnect=false";

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConfigString));

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConfigString;
            options.InstanceName = "vfr:";
        });

        services.AddSingleton<ICacheService, CacheService>();


        // ── 8. Utilities ──────────────────────────────────────────────────────
        services.AddSingleton<IDateTime, DateTimeService>();


        // ── Stripe Configuration ───────────────────────────────────────────────
        services.Configure<StripeSettings>(
            configuration.GetSection(StripeSettings.SectionName));

        // ── Application Services ──────────────────────────────────────────────

        // IPaymentGatewayService — Scoped: one instance per HTTP request.
        // Uses Polly resilience pipeline "stripe" (registered in P-049).
        services.AddScoped<IPaymentGatewayService, StripePaymentGatewayService>();

        // IEncryptionService — Singleton: stateless, thread-safe AES-256 service.
        services.AddSingleton<IEncryptionService, AesEncryptionService>();


        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        services.AddScoped<SubscriptionPlanSeeder>();

        services.AddScoped<IOrderRepository, OrderRepository>();  

        services.AddScoped<IInventoryRepository, InventoryRepository>();

        services.AddSignalR();
        services.AddScoped<INotificationHub, NotificationHubService>();


        // ── Analytics / Dashboard (P-041) ────────────────────────────────────────────

        // IDashboardRepository — scoped (one per request, uses scoped IApplicationDbContext).
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        // IS3StorageService — scoped (holds no mutable state per request).
        services.AddScoped<IS3StorageService, S3StorageService>();

        // IReportQueue — singleton (shared Channel between HTTP requests and BackgroundService).
        services.AddSingleton<IReportQueue, ReportQueue>();

        // ReportGenerationJob — hosted service (singleton, reads from IReportQueue.ReadAllAsync).
        services.AddHostedService<ReportGenerationJob>();

        services.AddScoped<IPlanLimitService, PlanLimitService>();

        return services;
    }
}