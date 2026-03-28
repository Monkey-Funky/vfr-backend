namespace Infrastructure;

using Microsoft.AspNetCore.Identity;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ─────────────────────────────────────────────────────────
        // CORRECTED: UseNpgsql replaces the original UseSqlServer.
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql
                    .MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null)));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        // ── Repository & Unit of Work ─────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // ── ASP.NET Core Identity ─────────────────────────────────────────────
        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // ── Redis Cache ───────────────────────────────────────────────────────
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:Configuration"];
            options.InstanceName = configuration["Redis:InstanceName"];
        });

        services.AddScoped<ICacheService, CacheService>();

        // ── Utilities ─────────────────────────────────────────────────────────
        services.AddSingleton<IDateTime, DateTimeService>();

        return services;
    }
}