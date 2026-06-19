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
        //
        // UseSnakeCaseNamingConvention() is chained here on DbContextOptionsBuilder.
        // This is the ONLY correct location — it must NOT be called on ModelBuilder
        // inside OnModelCreating (that causes a compile error).
        // Requires NuGet: EFCore.NamingConventions
        // Use pooled DbContext factory for significantly improved throughput.
        // AddDbContextPool reuses DbContext instances across requests instead of
        // allocating/disposing on every request — reduces GC pressure and
        // connection establishment overhead by 3-5× under concurrent load.
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
                .UseSnakeCaseNamingConvention());   // ← correct: on DbContextOptionsBuilder

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        // ── 2. Options bindings ───────────────────────────────────────────────
        services.Configure<JwtSettings>(options =>
    configuration.GetSection("JwtSettings").Bind(options));

        services.Configure<EmailSettings>(options =>
            configuration.GetSection("Email").Bind(options));

        // S3Settings — لسه محتاجينه علشان IS3StorageService (reports)
        services.Configure<S3Settings>(options =>
            configuration.GetSection("S3").Bind(options));

        // Cloudinary Settings — الجديد للصور
        services.Configure<CloudinarySettings>(options =>
            configuration.GetSection("Cloudinary").Bind(options));

        services.Configure<GoogleSettings>(options =>
            configuration.GetSection("Google").Bind(options));

        services.Configure<FalAiSettings>(options =>
            configuration.GetSection("FalAi").Bind(options));

        services.Configure<VirtualTryOn2DSettings>(options =>
            configuration.GetSection("VirtualTryOn2D").Bind(options));

        services.Configure<CatVtonSettings>(options =>
            configuration.GetSection("CatVTON").Bind(options));

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
            // NOTE: No Retry here — fal.ai calls are expensive ($0.02 each) and
            // non-idempotent. Retrying the full pipeline (objects + align) on failure
            // doubles costs and hits the timeout. Errors surface as 503 to the caller.
            builder
                .AddTimeout(TimeSpan.FromSeconds(300))  // FIX (Issue 5): worst case = 2x MaxPollSeconds(120s)=240s; 300s gives 60s headroom
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0,
                    MinimumThroughput = 3,               // open only after 3 consecutive failures
                    SamplingDuration = TimeSpan.FromSeconds(60),
                    BreakDuration = TimeSpan.FromSeconds(30)
                });
        });

        // ── 3a. 2D try-on resilience pipeline (FASHN model) ───────────────────
        //
        // Separate from "tryon" (3D) so that a spike of 3D failures does not
        // trip the 2D circuit breaker and vice-versa. Same non-retry rationale:
        // FASHN calls are non-idempotent ($0.02 each) — a single authoritative
        // attempt is correct. Timeout is shorter than 3D because FASHN is one
        // fal.ai call vs the 3D pipeline's two sequential calls.
        services.AddResiliencePipeline("tryon-2d", builder =>
        {
            builder
                .AddTimeout(TimeSpan.FromSeconds(120))  // single fal.ai call; 120s is generous
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0,
                    MinimumThroughput = 3,               // open only after 3 consecutive failures
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

        // ── 3d. Named HttpClient for fal.ai SAM 3D API ──────────────────────────
        //
        // Shared by FalAiQueueClient (3D SAM pipeline) and FalAiVirtualTryOn2DService
        // (2D FASHN pipeline). Timeout is set to 180s to accommodate the longest
        // possible fal.ai polling cycle (two sequential SAM 3D calls). Both pipelines
        // rely on Polly ("tryon" / "tryon-2d") for their own timeout enforcement;
        // the HttpClient timeout is a safety net for network-level hangs.
        services.AddHttpClient("fal-ai", (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(180);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // ── 3e. Named HttpClient for CatVTON (Hugging Face Spaces) ───────────────
        //
        // Used by CatVtonVirtualTryOn2DService. Timeout is set to 300s to accommodate
        // HF Space cold starts (30-60s) plus inference time (~60-120s).
        services.AddHttpClient("catvton", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(300);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // ── 4. S3-Compatible Client (AWS S3 / Cloudflare R2) ─────────────────
        //
        // لسه محتاجين S3 client علشان IS3StorageService (reports).
        // الـ client ده بيستخدمه S3StorageService بس — مش الصور.
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
        //
        // ده الـ client الجديد للصور (brand logos, products, categories, etc.)
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

        // ✅ Cloudinary
        services.AddScoped<IFileStorageService, CloudinaryFileStorageService>();

        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<ISizeRecommendationService, SizeRecommendationService>();
        services.AddScoped<IVirtualTryOnService, VirtualTryOnService>();
        services.AddScoped<IFalAiService, FalAiService>();
        services.AddScoped<IFalAiQueueClient, FalAiQueueClient>();
        services.AddScoped<IVirtualTryOn2DService, CatVtonVirtualTryOn2DService>();

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


        // ── Analytics / Dashboard (P-041) ────────────────────────────────────────────
        services.AddScoped<IDashboardRepository, DashboardRepository>();

        // IS3StorageService — لسه شغال للـ reports (مش الصور)
        services.AddScoped<IS3StorageService, S3StorageService>();

        services.AddSingleton<IReportQueue, ReportQueue>();
        services.AddHostedService<ReportGenerationJob>();

        // ── Subscription Lifecycle Jobs ──────────────────────────────────────────
        services.AddHostedService<SubscriptionExpiryJob>();
        services.AddHostedService<RecurringPaymentJob>();

        services.AddScoped<IPlanLimitService, PlanLimitService>();

        // Payment for Customer
        services.AddScoped<IStripeService, StripeService>();

        return services;
    }
}