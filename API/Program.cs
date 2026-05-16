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

if (!string.IsNullOrWhiteSpace(privateKeyPem))
{
    // ── PRODUCTION / DEVELOPMENT with configured keys ─────────────────────────
    //
    // ROOT CAUSE FIX: Environment variables on Render.com / Windows store newlines
    // as the two-character sequence backslash-n (\\n) rather than an actual newline
    // character (\n / 0x0A). ImportFromPem requires real newlines between the
    // base-64 lines and the PEM header/footer — if they are missing it throws:
    //   "No supported key formats were found."
    //
    // We also normalise \r\n → \n so CRLF files (edited on Windows) work correctly
    // in both development and production.
    //
    privateKeyPem = privateKeyPem
        .Replace("\\n", "\n")   // literal backslash-n  →  real newline  (env-var edge case)
        .Replace("\r\n", "\n")  // CRLF                 →  LF            (Windows file edge case)
        .Trim();                // remove any surrounding whitespace / stray blank lines

    try
    {
        rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem.AsSpan());
        Log.Information("JWT: RSA key loaded from configuration — tokens survive restarts.");
    }
    catch (ArgumentException ex)
    {
        // Provide a clear diagnostic message instead of the cryptic framework one.
        Log.Fatal(ex,
            "JWT: ImportFromPem failed. " +
            "Verify that JwtSettings:PrivateKeyPem contains the full PEM text " +
            "(including -----BEGIN PRIVATE KEY----- / -----END PRIVATE KEY----- lines) " +
            "with real newlines, not literal \\n characters. " +
            "On Render.com use a Secret File or replace \\n with actual newlines in the env var.");
        throw;
    }
}
else
{
    // ── DEVELOPMENT: Auto-generate ephemeral RSA keys ─────────────────────────
    //
    // Tokens are invalidated on every restart — acceptable for local dev.
    // To get persistent tokens locally, add JwtSettings:PrivateKeyPem to
    // appsettings.Development.json (see private.pem at the solution root).
    //
    rsa = RSA.Create(keySizeInBits: 2048);
    Log.Warning("JWT: Using ephemeral RSA keys (tokens invalidated on restart). " +
                "Set JwtSettings:PrivateKeyPem to make tokens survive restarts.");
}

// ── ROOT CAUSE FIX: Single RsaSecurityKey singleton shared between signing and validation ──
//
// PROBLEM (was): Program.cs created  new RsaSecurityKey(rsa)  for validation,
//               while TokenService created its OWN new RsaSecurityKey(_signingRsa)
//               for signing. Both wrapped the same RSA object but were different
//               instances with no KeyId set.  The JWT library therefore could not
//               find a matching key when the token's `kid` header was absent, and
//               IDX10517 "Signature validation failed – kid missing" was thrown.
//
// FIX: Create ONE RsaSecurityKey instance with a stable, deterministic KeyId
//      (SHA-256 thumbprint of the SubjectPublicKeyInfo DER bytes, base64url-encoded
//      per RFC 7638).  Register it as a singleton so TokenService and the JwtBearer
//      middleware use the EXACT SAME object — same KeyId, same key material, same
//      CryptoProviderFactory entry.  The `kid` claim is now written into every JWT
//      header, and validation matches it immediately without trying all keys.
//
var keyId = Convert.ToBase64String(
    SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))
    .Replace("+", "-").Replace("/", "_").TrimEnd('='); // base64url, RFC 7638 style

var rsaSecurityKey = new RsaSecurityKey(rsa) { KeyId = keyId };

// Register BOTH the RSA instance and the RsaSecurityKey as singletons.
// TokenService will inject RsaSecurityKey directly — no more dual-instance problem.
builder.Services.AddSingleton(rsa);
builder.Services.AddSingleton(rsaSecurityKey);

Log.Information("JWT: RsaSecurityKey registered with KeyId={KeyId}", keyId);

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
        IssuerSigningKey = rsaSecurityKey, // The singleton — same object used for signing
        ValidAlgorithms = ["RS256"],       // Explicit whitelist — prevents alg:none attack
        ClockSkew = TimeSpan.Zero
    };

    // ── FIX: Handle "Bearer Bearer <token>" sent by some API clients ──────────
    //
    // Swagger UI's Http/bearer scheme automatically prepends "Bearer " to whatever
    // the user types in the Authorize dialog.  If the user also types "Bearer "
    // manually (a very common mistake) the header arrives as:
    //   Authorization: Bearer Bearer eyJhbGci...
    // which fails validation.  The event below strips the duplicate prefix so that
    // the token is accepted regardless of whether the user included "Bearer " or not.
    //
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                // Collapse "Bearer Bearer <token>" → "Bearer <token>"
                const string prefix = "Bearer ";
                var token = authHeader;
                while (token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    token = token[prefix.Length..].TrimStart();

                context.Token = token;
            }
            return Task.CompletedTask;
        },

        OnAuthenticationFailed = context =>
        {
            // Surface the real failure reason in development logs so the root
            // cause is obvious without attaching a debugger.
            Log.Warning("JWT authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        }
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


    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
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
        // Paste ONLY the token — do NOT type "Bearer " yourself. Swagger adds it automatically.
        // Correct:  eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
        // Wrong:    Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9... (gives 401!)
        Description = "Paste your JWT token ONLY — do NOT add Bearer yourself. Swagger adds it automatically.",
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