using System.Net.Http.Headers;
using Amazon;
using Amazon.S3;
using Application.Interfaces.External;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Application.Interfaces.Services.Customer;
using CloudinaryDotNet;
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
using Infrastructure.Services;

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
        services.AddDbContextPool<ApplicationDbContext>(options =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    npgsql => npgsql
                        .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                        .EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorCodesToAdd: null))
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // ── 2. Options bindings ───────────────────────────────────────────────
        services.Configure<JwtSettings>(options =>
    configuration.GetSection("JwtSettings").Bind(options));

        services.Configure<EmailSettings>(options =>
            configuration.GetSection("Email").Bind(options));

        services.Configure<S3Settings>(options =>
            configuration.GetSection("S3").Bind(options));

        services.Configure<CloudinarySettings>(options =>
            configuration.GetSection("Cloudinary").Bind(options));

        services.Configure<GoogleSettings>(options =>
            configuration.GetSection("Google").Bind(options));

        services.Configure<FalAiSettings>(options =>
            configuration.GetSection("FalAi").Bind(options));

        services.Configure<VirtualTryOn2DSettings>(options =>
            configuration.GetSection("VirtualTryOn2D").Bind(options));

        services.Configure<AiGenerationSettings>(options =>
            configuration.GetSection("AiGeneration").Bind(options));

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

        // 3D try-on pipeline (sam-3/3d-objects → sam-3/3d-align).
        // sam-3/3d-objects can take up to ~600s for complex garments;
        // sam-3/3d-align is typically 10-20s.
        // Polly timeout = 950s covers 700s objects + 120s align + 130s buffer.
        // No retry: fal.ai calls are non-idempotent ($0.02 each).
        services.AddResiliencePipeline("tryon", builder =>
        {
            builder
                .AddTimeout(TimeSpan.FromSeconds(950))
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0,
                    MinimumThroughput = 3,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    BreakDuration = TimeSpan.FromSeconds(30)
                });
        });

        // 2D try-on pipeline (FASHN single call).
        // VirtualTryOn2DSettings.TimeoutSeconds = 180s (poll budget);
        // Polly timeout = 250s gives 70s headroom above the poll budget.
        services.AddResiliencePipeline("tryon-2d", builder =>
        {
            builder
                .AddTimeout(TimeSpan.FromSeconds(250))
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0,
                    MinimumThroughput = 3,
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    BreakDuration = TimeSpan.FromSeconds(30)
                });
        });

        // ── 3b. Stripe resilience pipeline ─────────────────────────────────────
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

        // ── 3c. External API resilience (Weather + AI Suggestions) ─────────────
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

        services.AddHttpClient<IBodyMeasurementExtractionService, BodyMeasurementExtractionService>()
            .AddResilienceHandler("ai-extraction-api", builder =>
            {
                builder
                    .AddTimeout(TimeSpan.FromSeconds(60))
                    .AddRetry(new Microsoft.Extensions.Http.Resilience.HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 2,
                        Delay = TimeSpan.FromSeconds(2),
                        BackoffType = DelayBackoffType.Exponential
                    });
            });

        // Named HttpClient shared by FalAiQueueClient (3D SAM) and
        // FalAiVirtualTryOn2DService (2D FASHN).
        // Timeout = 1000s: must exceed the longest possible polling session
        // (sam-3/3d-objects ObjectsApiPollSeconds = 700s) with generous headroom.
        // Polly pipelines enforce the real timeout; this is a connection-level safety net.
        services.AddHttpClient("fal-ai", (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(1000);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // ── 4. S3-Compatible Client (AWS S3 / Cloudflare R2) ─────────────────
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
                var config = new AmazonS3Config
                {
                    ServiceURL = serviceUrl,
                    ForcePathStyle = true,
                    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED,
                };

                return new AmazonS3Client(accessKey, secretKey, config);
            }

            return new AmazonS3Client(RegionEndpoint.GetBySystemName(region));
        });

        // ── 4b. Cloudinary Client ────────────────────────────────────────────
        services.AddSingleton<Cloudinary>(sp =>
        {
            var cloudinaryConfig = configuration.GetSection("Cloudinary");
            var cloudName = cloudinaryConfig["CloudName"];
            var apiKey = cloudinaryConfig["ApiKey"];
            var apiSecret = cloudinaryConfig["ApiSecret"];

            var account = new Account(cloudName, apiKey, apiSecret);
            return new Cloudinary(account) { Api = { Secure = true } };
        });

        // ── 5. Application services ───────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();

        services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();

        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<ISizeRecommendationService, SizeRecommendationService>();
        services.AddScoped<IVirtualTryOnService, VirtualTryOnService>();
        services.AddScoped<IFalAiService, FalAiService>();
        services.AddScoped<IFalAiQueueClient, FalAiQueueClient>();
        services.AddScoped<IVirtualTryOn2DService, FalAiVirtualTryOn2DService>();
        services.AddScoped<IAiGenerationCacheService, AiGenerationCacheService>();

        // ── 6. Repository & Unit of Work ──────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // ── 7. Redis / Distributed Cache ──────────────────────────────────────
        var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";

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
        services.AddScoped<IPaymentGatewayService, StripePaymentGatewayService>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        services.AddScoped<IOutfitSuggestionService, MockOutfitSuggestionService>();
        services.AddHttpClient<IComplementaryStyleService, ComplementaryStyleService>();

        services.AddScoped<SubscriptionPlanSeeder>();
        services.AddScoped<ExcelDataSeeder>();
        services.AddScoped<TransactionalDataSeeder>();
        services.AddScoped<TestAccountSeeder>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        services.AddScoped<IInventoryRepository, InventoryRepository>();

        services.AddSignalR();
        services.AddScoped<INotificationHub, NotificationHubService>();

        // ── Analytics / Dashboard ────────────────────────────────────────────────────
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        services.AddScoped<IS3StorageService, S3StorageService>();

        services.AddSingleton<IReportQueue, ReportQueue>();
        services.AddHostedService<ReportGenerationJob>();

        // ── Subscription Lifecycle Jobs ──────────────────────────────────────────
        services.AddHostedService<SubscriptionExpiryJob>();
        services.AddHostedService<RecurringPaymentJob>();

        services.AddScoped<IPlanLimitService, PlanLimitService>();

        services.AddScoped<IStripeService, StripeService>();

        return services;
    }
}
