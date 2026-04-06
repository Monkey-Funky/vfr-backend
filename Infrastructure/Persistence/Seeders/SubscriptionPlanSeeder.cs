// src/Infrastructure/Persistence/Seeders/SubscriptionPlanSeeder.cs

namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeds the seven canonical subscription plans into the database.
///
/// DESIGN RULES:
///   • Idempotent — uses INSERT ... ON CONFLICT (id) DO NOTHING.
///     Running this seeder ten times produces the same result as running it once.
///   • Uses ExecuteSqlAsync(FormattableString) — EF Core 9's interpolated SQL API.
///     Each {expression} is compiled into a strongly-typed DbParameter by Npgsql,
///     eliminating the type-inference bugs that affect ExecuteSqlRawAsync + object[].
///   • Fixed GUIDs are constants, not generated at runtime.
///     They MUST match StartTrialCommandHandler.TrialPlanId and any other place
///     in the codebase that hard-references a specific plan ID.
///   • Idempotency is checked by ID, not by count — so partial-seed states
///     (e.g. after a partial rollback) are always recovered correctly.
/// </summary>
internal sealed class SubscriptionPlanSeeder : ISeeder
{
    // ── Fixed Plan GUIDs — single source of truth ──────────────────────────────
    //
    // Any command handler that needs to reference a specific plan by ID should
    // import these constants (or copy them with a comment pointing here).

    internal static readonly Guid BasicMonthlyId = new("11111111-1111-1111-1111-111111111001");
    internal static readonly Guid BasicYearlyId = new("11111111-1111-1111-1111-111111111002");
    internal static readonly Guid StandardMonthlyId = new("22222222-2222-2222-2222-222222222001");
    internal static readonly Guid StandardYearlyId = new("22222222-2222-2222-2222-222222222002");
    internal static readonly Guid EnterpriseMthlyId = new("33333333-3333-3333-3333-333333333001");
    internal static readonly Guid EnterpriseYearId = new("33333333-3333-3333-3333-333333333002");
    internal static readonly Guid SaasPlanId = new("44444444-4444-4444-4444-444444444001");

    /// <summary>All seven fixed plan IDs for fast existence checks.</summary>
    private static readonly IReadOnlyList<Guid> AllPlanIds =
    [
        BasicMonthlyId, BasicYearlyId,
        StandardMonthlyId, StandardYearlyId,
        EnterpriseMthlyId, EnterpriseYearId,
        SaasPlanId,
    ];

    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SubscriptionPlanSeeder> _logger;

    public SubscriptionPlanSeeder(
        ApplicationDbContext context,
        ILogger<SubscriptionPlanSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ── Public Entry Point ────────────────────────────────────────────────────

    /// <summary>
    /// Seeds all seven subscription plans. Safe to call multiple times (idempotent).
    /// Only inserts the plans that are genuinely missing — handles partial-seed states.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // ── Fast-path: check which specific plan IDs already exist ────────────
        //
        // Using ID-based lookup (not count) so partial-seed states are recovered:
        // if 5 of 7 plans exist, the seeder inserts exactly the 2 that are missing.

        var existingIds = await _context.SubscriptionPlans
            .IgnoreQueryFilters()                    // bypass soft-delete global filter
            .Where(p => AllPlanIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (existingIds.Count >= AllPlanIds.Count)
        {
            _logger.LogDebug(
                "SubscriptionPlanSeeder: all {Count} plans already present, skipping.",
                AllPlanIds.Count);
            return;
        }

        // Determine which plans are genuinely missing.
        var missingPlans = BuildPlans()
            .Where(p => !existingIds.Contains(p.Id))
            .ToList();

        _logger.LogInformation(
            "SubscriptionPlanSeeder: inserting {Count} missing plan(s)...",
            missingPlans.Count);

        // ── Insert missing plans inside a single transaction ──────────────────

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var plan in missingPlans)
            {
                // ────────────────────────────────────────────────────────────────
                // WHY ExecuteSqlAsync(FormattableString) instead of ExecuteSqlRawAsync?
                //
                // ExecuteSqlRawAsync(sql, params object[]) boxes every C# value to
                // `object`. Npgsql must then infer the PostgreSQL type from the boxed
                // value, which fails silently or throws for bool, decimal, and
                // nullable int when passed alongside a {0}::uuid cast expression.
                //
                // ExecuteSqlAsync(FormattableString) compiles each {expression}
                // into a DbParameter with the exact CLR type preserved. Npgsql
                // maps them deterministically:
                //   Guid     → uuid        (no ::uuid cast needed)
                //   string   → text
                //   decimal  → numeric
                //   int?     → integer or NULL (null is handled automatically)
                //   bool     → boolean
                //
                // This is the EF Core 9 recommended API for parameterized raw SQL.
                // ────────────────────────────────────────────────────────────────

                await _context.Database.ExecuteSqlAsync(
                    $"""
                     INSERT INTO subscription_plans
                     (
                         id,                   name,                 tier,
                         billing_cycle,        price_amount,         currency,
                         commission_rate,      max_active_products,  max_monthly_try_ons,
                         support_level,        is_active,            is_white_label,
                         includes_source_code, includes_mobile_apps, has_sla,
                         has_dedicated_team,   created_at
                     )
                     VALUES
                     (
                         {plan.Id},                  {plan.Name},               {plan.Tier},
                         {plan.BillingCycle},        {plan.PriceAmount},        {plan.Currency},
                         {plan.CommissionRate},      {plan.MaxActiveProducts},  {plan.MaxMonthlyTryOns},
                         {plan.SupportLevel},        {plan.IsActive},           {plan.IsWhiteLabel},
                         {plan.IncludesSourceCode},  {plan.IncludesMobileApps}, {plan.HasSla},
                         {plan.HasDedicatedTeam},    now()
                     )
                     ON CONFLICT (id) DO NOTHING
                     """,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "SubscriptionPlanSeeder: {Count} plan(s) seeded successfully.",
                missingPlans.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "SubscriptionPlanSeeder: seeding failed — transaction rolled back.");
            throw;
        }
    }

    // ── Private Plan Definitions ──────────────────────────────────────────────

    /// <summary>
    /// Returns the seven canonical plans as lightweight data records.
    /// These are NOT domain entities — only used here for seeding.
    /// Edit this list when plan details change (price, limits, etc.).
    /// </summary>
    private static IEnumerable<PlanSeedRow> BuildPlans()
    {
        // ── Basic ──────────────────────────────────────────────────────────────
        yield return new PlanSeedRow(
            Id: BasicMonthlyId,
            Name: "Basic Monthly",
            Tier: "Basic",
            BillingCycle: "Monthly",
            PriceAmount: 50m,
            Currency: "USD",
            CommissionRate: 0.0500m,
            MaxActiveProducts: 250,
            MaxMonthlyTryOns: 500,
            SupportLevel: "Email Support",
            IsActive: true,
            IsWhiteLabel: false,
            IncludesSourceCode: false,
            IncludesMobileApps: false,
            HasSla: false,
            HasDedicatedTeam: false
        );

        yield return new PlanSeedRow(
            Id: BasicYearlyId,
            Name: "Basic Yearly",
            Tier: "Basic",
            BillingCycle: "Yearly",
            PriceAmount: 500m,
            Currency: "USD",
            CommissionRate: 0.0500m,
            MaxActiveProducts: 250,
            MaxMonthlyTryOns: 500,
            SupportLevel: "Email Support",
            IsActive: true,
            IsWhiteLabel: false,
            IncludesSourceCode: false,
            IncludesMobileApps: false,
            HasSla: false,
            HasDedicatedTeam: false
        );

        // ── Standard ───────────────────────────────────────────────────────────
        yield return new PlanSeedRow(
            Id: StandardMonthlyId,
            Name: "Standard Monthly",
            Tier: "Standard",
            BillingCycle: "Monthly",
            PriceAmount: 150m,
            Currency: "USD",
            CommissionRate: 0.0300m,
            MaxActiveProducts: 1000,
            MaxMonthlyTryOns: 2500,
            SupportLevel: "Email & Chat Support",
            IsActive: true,
            IsWhiteLabel: false,
            IncludesSourceCode: false,
            IncludesMobileApps: false,
            HasSla: false,
            HasDedicatedTeam: false
        );

        yield return new PlanSeedRow(
            Id: StandardYearlyId,
            Name: "Standard Yearly",
            Tier: "Standard",
            BillingCycle: "Yearly",
            PriceAmount: 1500m,
            Currency: "USD",
            CommissionRate: 0.0300m,
            MaxActiveProducts: 1000,
            MaxMonthlyTryOns: 2500,
            SupportLevel: "Email & Chat Support",
            IsActive: true,
            IsWhiteLabel: false,
            IncludesSourceCode: false,
            IncludesMobileApps: false,
            HasSla: false,
            HasDedicatedTeam: false
        );

        // ── Enterprise ─────────────────────────────────────────────────────────
        yield return new PlanSeedRow(
            Id: EnterpriseMthlyId,
            Name: "Enterprise Monthly",
            Tier: "Enterprise",
            BillingCycle: "Monthly",
            PriceAmount: 500m,
            Currency: "USD",
            CommissionRate: 0.0150m,
            MaxActiveProducts: null,             // unlimited
            MaxMonthlyTryOns: null,             // unlimited
            SupportLevel: "Priority Support + SLA",
            IsActive: true,
            IsWhiteLabel: false,
            IncludesSourceCode: false,
            IncludesMobileApps: false,
            HasSla: true,
            HasDedicatedTeam: false
        );

        yield return new PlanSeedRow(
            Id: EnterpriseYearId,
            Name: "Enterprise Yearly",
            Tier: "Enterprise",
            BillingCycle: "Yearly",
            PriceAmount: 5000m,
            Currency: "USD",
            CommissionRate: 0.0150m,
            MaxActiveProducts: null,             // unlimited
            MaxMonthlyTryOns: null,             // unlimited
            SupportLevel: "Priority Support + SLA",
            IsActive: true,
            IsWhiteLabel: false,
            IncludesSourceCode: false,
            IncludesMobileApps: false,
            HasSla: true,
            HasDedicatedTeam: false
        );

        // ── SaaS / White-Label ─────────────────────────────────────────────────
        yield return new PlanSeedRow(
            Id: SaasPlanId,
            Name: "SaaS White-Label",
            Tier: "SaaS",
            BillingCycle: "SaaS",
            PriceAmount: 0m,               // custom pricing via enquiry
            Currency: "USD",
            CommissionRate: 0.0000m,
            MaxActiveProducts: null,             // unlimited
            MaxMonthlyTryOns: null,             // unlimited
            SupportLevel: "Dedicated Team + Custom SLA",
            IsActive: true,
            IsWhiteLabel: true,
            IncludesSourceCode: true,
            IncludesMobileApps: true,
            HasSla: true,
            HasDedicatedTeam: true
        );
    }

    // ── Private Data Record ───────────────────────────────────────────────────

    /// <summary>
    /// Internal data holder used only by BuildPlans().
    /// Not a domain entity — no invariant enforcement needed because these values
    /// are hard-coded constants, not user input.
    /// </summary>
    private sealed record PlanSeedRow(
        Guid Id,
        string Name,
        string Tier,
        string BillingCycle,
        decimal PriceAmount,
        string Currency,
        decimal CommissionRate,
        int? MaxActiveProducts,
        int? MaxMonthlyTryOns,
        string SupportLevel,
        bool IsActive,
        bool IsWhiteLabel,
        bool IncludesSourceCode,
        bool IncludesMobileApps,
        bool HasSla,
        bool HasDedicatedTeam
    );
}