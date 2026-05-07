using Amazon;
using Amazon.S3;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using Infrastructure.BackgroundJobs;
using Infrastructure.Hubs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Persistence.Seeders;
using Infrastructure.Services.Auth;
using Infrastructure.Services.Communication;
using Infrastructure.Services.Customer;
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

        services.AddResiliencePipeline("tryon", builder =>
        {
            builder
                .AddTimeout(TimeSpan.FromSeconds(30))
                .AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.FromSeconds(2)
                })
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0, // Fail on every threshold hit
                    MinimumThroughput = 3, // 3 failures
                    SamplingDuration = TimeSpan.FromSeconds(30), // in 30s
                    BreakDuration = TimeSpan.FromSeconds(30) // open 30s
                });
        });

        // ── 3a. Stripe resilience pipeline ─────────────────────────────────────
        //
        // Required by StripePaymentGatewayService. Retry with exponential backoff
        // and circuit breaker for Stripe API reliability.
        services.AddResiliencePipeline("stripe", builder =>
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
                    BreakDuration = TimeSpan.FromSeconds(30),
                });
        });

        // ── 3b. External API resilience (Weather + AI Suggestions) ─────────────
        //
        // W-1 Fix: Shared pipeline for IWeatherService / IOutfitSuggestionService.
        // When swapping mocks to real HttpClient implementations, register via:
        //   services.AddHttpClient<IWeatherService, RealWeatherService>()
        //           .AddResilienceHandler("external-api", ...);
        services.AddHttpClient<IWeatherService, WeatherService>()
            .AddResilienceHandler("external-api", builder =>
            {
                builder
                    .AddTimeout(TimeSpan.FromSeconds(10))
                .AddRetry(new Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                })
                .AddCircuitBreaker(new Microsoft.Extensions.Http.Resilience.HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30),
                });
            });

        // Register the AI Extraction service with a longer timeout (AI processing can take 10-30s)
        services.AddHttpClient<IBodyMeasurementExtractionService, BodyMeasurementExtractionService>()
            .AddResilienceHandler("ai-extraction-api", builder =>
            {
                builder
                    .AddTimeout(TimeSpan.FromSeconds(60)) // Longer timeout for image processing
                    .AddRetry(new Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 2,
                        Delay = TimeSpan.FromSeconds(2),
                        BackoffType = DelayBackoffType.Exponential
                    });
            });

        // ── 4. S3-Compatible Client (AWS S3 / Cloudflare R2) ─────────────────
        //
        // Cloudflare R2 is fully S3-compatible. When S3:ServiceUrl is set,
        // the client uses that endpoint instead of AWS. This enables
        // the same FileStorageService to work with both AWS S3 and R2.
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var s3Config = configuration.GetSection("S3");
            var serviceUrl = s3Config["ServiceUrl"];
            var accessKey = s3Config["AccessKey"];
            var secretKey = s3Config["SecretKey"];
            var region = s3Config["Region"] ?? "us-east-1";

            if (!string.IsNullOrWhiteSpace(serviceUrl) &&
                !string.IsNullOrWhiteSpace(accessKey) &&
                !string.IsNullOrWhiteSpace(secretKey))
            {
                // ── Cloudflare R2 / Custom S3-compatible endpoint ──────────────
                var config = new AmazonS3Config
                {
                    ServiceURL = serviceUrl,
                    ForcePathStyle = true,  // R2 requires path-style addressing
                    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED,
                };

                return new AmazonS3Client(accessKey, secretKey, config);
            }

            // ── Default AWS S3 (for development with IAM roles) ────────────────
            return new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
        });

        // ── 5. Application services ───────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<ISizeRecommendationService, SizeRecommendationService>();
        services.AddScoped<IVirtualTryOnService, VirtualTryOnService>();

        // ── 6. Repository & Unit of Work ──────────────────────────────────────
        // These were incorrectly commented out — they are required by all command handlers.
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // ── 7. Redis / Distributed Cache ──────────────────────────────────────
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

        
        services.AddScoped<IOutfitSuggestionService, MockOutfitSuggestionService>();

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