

using Application.Interfaces.Services;
using Infrastructure.Hubs;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);


var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");


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
// DEVELOPMENT SIMPLIFICATION:
// RSA keys are auto-generated every time the app starts.
// No OpenSSL, no user-secrets, no .pem files needed.
//
// ⚠ Trade-off: any JWT token you got before a restart will be INVALID
//   after restart because the key changes. Just log in again to get a
//   new token. This is perfectly fine during development.
//
// When you are ready for production, replace this block with keys
// loaded from environment variables or Azure Key Vault.

var jwtSettings = builder.Configuration.GetSection("JwtSettings");

// // Generate a fresh RSA key pair in memory
// var rsa = RSA.Create(keySizeInBits: 2048);

// // Register the RSA instance as a singleton so TokenService (P-012) can
// // use the SAME private key for signing that Program.cs uses for validation.
// // Without this, signing and validation use different keys → 401 on every request.
// builder.Services.AddSingleton(rsa);

// ── RSA Key Setup ────────────────────────────────────────────────────────
RSA rsa;
if (builder.Environment.IsProduction())
{
    // Production: load fixed RSA keys from environment variables
    var privateKeyPem = Environment.GetEnvironmentVariable("JWT_PRIVATE_KEY_PEM")
        ?? throw new InvalidOperationException("JWT_PRIVATE_KEY_PEM environment variable is not set.");
    
    rsa = RSA.Create();
    rsa.ImportFromPem(privateKeyPem.ToCharArray());
}
else
{
    // Development: auto-generate keys (existing behavior)
    rsa = RSA.Create(keySizeInBits: 2048);
}
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
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
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
        Description = "Virtual Fitting Room — Retailer Module API"
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
});

// ── 8. BUILD ─────────────────────────────────────────────────────────────────
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




// ── 9. MIDDLEWARE PIPELINE ───────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Swagger FIRST — before rate limiting so /swagger/* is never throttled
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI(c =>
//    {
//        c.SwaggerEndpoint("/swagger/v1/swagger.json", "VFR Retailer API v1");
//        c.RoutePrefix = string.Empty;
//    });
//}

// Enable Swagger in all environments for graduation project demo
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "VFR Retailer API v1");
    c.RoutePrefix = string.Empty;
});

app.UseIpRateLimiting(); // ← after swagger

app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
});

//app.UseHttpsRedirection();

// In production behind Render's reverse proxy, HTTPS is handled externally
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Trust proxy headers (Render uses reverse proxy)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});


app.UseCors("VfrCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// ── 10. RUN ──────────────────────────────────────────────────────────────────
try
{
    Log.Information("Starting VFR Retailer API — Development mode (auto-generated RSA keys)");
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