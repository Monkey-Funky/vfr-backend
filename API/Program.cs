using Application.Interfaces.Services;
using Infrastructure.Hubs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// ── 1. SERILOG ───────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "Logs/vfr-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ── 2. CORE SERVICES ─────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
{
opts.JsonSerializerOptions.PropertyNamingPolicy =
    System.Text.Json.JsonNamingPolicy.CamelCase;
opts.JsonSerializerOptions.DefaultIgnoreCondition =
    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

// ── 3. APPLICATION & INFRASTRUCTURE ─────────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// CurrentUserService lives in API layer — registered here
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ── 4. JWT RS256 AUTHENTICATION ──────────────────────────────────────────────
//
// PRODUCTION FLOW:
//   RSA keys are loaded from configuration (environment variables on Render).
//   This ensures tokens survive restarts — no more "log in again after deploy".
//
// DEVELOPMENT FLOW:
//   If no keys are configured, auto-generate ephemeral keys (dev convenience).
//   Trade-off: JWTs invalidated on restart. Fine for local development.

var jwtSettings = builder.Configuration.GetSection("JwtSettings");

RSA rsa;
var privateKeyPem = jwtSettings["PrivateKeyPem"];
var publicKeyPem = jwtSettings["PublicKeyPem"];

if (!string.IsNullOrWhiteSpace(privateKeyPem))
{
    // ── PRODUCTION: Load RSA keys from configuration / environment variables ──
    rsa = RSA.Create();
    rsa.ImportFromPem(privateKeyPem.AsSpan());
    Log.Information("JWT: Using RSA keys from configuration (tokens survive restarts).");
}
else
{
    // ── DEVELOPMENT: Auto-generate ephemeral RSA keys ─────────────────────────
    rsa = RSA.Create(keySizeInBits: 2048);
    Log.Warning("JWT: Using auto-generated RSA keys (tokens invalidated on restart). " +
                "Set JwtSettings:PrivateKeyPem for production.");
}

// Register the RSA instance as a singleton so TokenService uses the SAME
// private key for signing that Program.cs uses for validation.
builder.Services.AddSingleton(rsa);

var rsaSecurityKey = new RsaSecurityKey(rsa);

builder.Services.AddAuthentication(options =>
{
options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtSettings["Issuer"],
    ValidAudience = jwtSettings["Audience"],
    IssuerSigningKey = rsaSecurityKey,
    ValidAlgorithms = ["RS256"],   // Explicit algorithm whitelist — prevents alg:none attack
    ClockSkew = TimeSpan.Zero
};
});

builder.Services.AddAuthorization();

// ── 5. RATE LIMITING ─────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(
    builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(
    builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// ── 6. CORS ──────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
options.AddPolicy("VfrCors", policy =>
{
if (builder.Environment.IsDevelopment())
{
    // Development: allow everything so Swagger / Postman / frontend all work
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader();
}
else
{
    // Production: read from configuration, fall back to allow all
    // until the frontend team deploys their app.
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>();

    if (allowedOrigins is { Length: > 0 })
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    }
    else
    {
        // No frontend URLs configured yet — allow all origins temporarily.
        // IMPORTANT: Restrict this once the frontend is deployed.
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    }
}
});
});

// ── 7. SWAGGER ───────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
c.EnableAnnotations();

// FIX (F-Swagger-A): IFormFile must be explicitly mapped to a binary schema.
// Without this, Swashbuckle 7.x throws an InvalidOperationException during
// startup when it encounters IFormFile on RegisterStep2Request.BrandLogoFile.
// That exception is caught by ExceptionHandlingMiddleware, which returns
// {"code":"INTERNAL_ERROR",...} — a JSON body with no openapi version field —
// causing Swagger UI to display "does not specify a valid version field".
c.MapType<IFormFile>(() => new Microsoft.OpenApi.Models.OpenApiSchema
{
    Type = "string",
    Format = "binary"
});

// Safety net: if any action ever exposes Stream in its schema,
// map it to a binary file field instead of crashing.
c.MapType<Stream>(() => new Microsoft.OpenApi.Models.OpenApiSchema
{
    Type = "string",
    Format = "binary"
});

c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
{
    Title = "VFR Retailer API",
    Version = "v1",
    Description = "Virtual Fitting Room — Retailer Module API. " +
                  "Use the Authorize button to paste your JWT Bearer token."
});

c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
{
    Description = "Paste your JWT token here. Example: Bearer eyJhbGci...",
    Name = "Authorization",
    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT"
});

c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            []
        }
    });
    c.CustomSchemaIds(type => type.FullName); // Use full type names to avoid conflicts in Swagger schema IDs
});

// ── 8. RESPONSE COMPRESSION ─────────────────────────────────────────────────
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        ["application/json", "text/plain"]);
});

builder.Services.Configure<BrotliCompressionProviderOptions>(opts =>
    opts.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opts =>
    opts.Level = CompressionLevel.SmallestSize);

// ── 9. HEALTH CHECKS ────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: ["db", "ready"])
    .AddRedis(
        builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379",
        name: "redis",
        tags: ["cache", "ready"]);

// ── 10. FORWARDED HEADERS (Render.com proxy) ─────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(opts =>
{
    opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opts.KnownNetworks.Clear();
    opts.KnownProxies.Clear();
});

// ── 11. BUILD ────────────────────────────────────────────────────────────────
var app = builder.Build();


// Seed data : 

// ── 1. Apply pending EF Core migrations automatically ─────────────────────────
//
// In production you may prefer to run migrations via a deployment pipeline
// instead of at startup. If so, remove this block and run:
//   dotnet ef database update --project Infrastructure --startup-project API
//
using (var scope = app.Services.CreateScope())
{
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
await db.Database.MigrateAsync();
}

// ── 2. Seed reference data ────────────────────────────────────────────────────
//
// Runs all seeders registered in DatabaseSeeder.
// Safe to call on every startup — every seeder is idempotent.
await DatabaseSeeder.SeedAsync(app.Services);




// ── 12. MIDDLEWARE PIPELINE ──────────────────────────────────────────────────

// Forwarded headers MUST be first — before any middleware that reads
// Request.Scheme or RemoteIpAddress (rate limiting, logging, auth).
app.UseForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Response compression — before any content-producing middleware
app.UseResponseCompression();

// Swagger enabled in ALL environments so frontend team can integrate
// via the Render URL. Protected by JWT where needed.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VFR Retailer API v1");
    c.RoutePrefix = string.Empty;  // Swagger at root URL
    c.DocumentTitle = "VFR API — Swagger";
    c.DefaultModelsExpandDepth(-1);  // Collapse schemas section for cleaner UI
});

app.UseIpRateLimiting(); // ← after swagger

app.UseSerilogRequestLogging(opts =>
{
opts.MessageTemplate =
    "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});

// Do NOT use HTTPS redirection — Render.com terminates TLS at the proxy.
// app.UseHttpsRedirection();

app.UseCors("VfrCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// ── Health check endpoint ─────────────────────────────────────────────────────
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds + "ms",
                exception = e.Value.Exception?.Message
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds + "ms"
        });
        await context.Response.WriteAsync(result);
    }
});

// ── 13. RUN ──────────────────────────────────────────────────────────────────
try
{
Log.Information("Starting VFR Retailer API — Environment: {Env}", app.Environment.EnvironmentName);
await app.RunAsync();
}
catch (Exception ex)
{
Log.Fatal(ex, "VFR Retailer API failed to start");
throw;
}
finally
{
await Log.CloseAndFlushAsync();
}

public partial class Program { }