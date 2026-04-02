using Domain.Common;
using Domain.Exceptions;

namespace Domain.Entities.Subscriptions;

/// <summary>
/// Represents a subscription tier (Basic, Standard, Enterprise, SaaS).
/// Global entity — not scoped to a retailer. Seeded in database migrations.
/// Rules:
///   - No public setters; all mutation via factory method Create().
///   - IsUnlimited is a computed domain property (no DB column).
/// </summary>
public sealed class SubscriptionPlan : BaseEntity
{
    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Display name, e.g. "Basic Monthly".</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Tier group, e.g. "Basic", "Standard", "Enterprise", "SaaS".</summary>
    public string Tier { get; private set; } = default!;

    /// <summary>"Monthly", "Yearly", or "SaaS".</summary>
    public string BillingCycle { get; private set; } = default!;

    /// <summary>Subscription price per billing cycle.</summary>
    public decimal PriceAmount { get; private set; }

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public string Currency { get; private set; } = default!;

    /// <summary>
    /// Platform commission rate applied to orders.
    /// Stored as decimal fraction: 0.0500 = 5%.
    /// </summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>Maximum concurrent active products. Null means unlimited.</summary>
    public int? MaxActiveProducts { get; private set; }

    /// <summary>Maximum VFR try-on sessions per month. Null means unlimited.</summary>
    public int? MaxMonthlyTryOns { get; private set; }

    /// <summary>Human-readable support tier description.</summary>
    public string SupportLevel { get; private set; } = default!;

    /// <summary>Whether this plan is available for selection by new retailers.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Grants white-label rights (SaaS tier only).</summary>
    public bool IsWhiteLabel { get; private set; }

    /// <summary>Includes platform source code (SaaS tier only).</summary>
    public bool IncludesSourceCode { get; private set; }

    /// <summary>Includes pre-built iOS/Android apps (SaaS tier only).</summary>
    public bool IncludesMobileApps { get; private set; }

    /// <summary>Service Level Agreement is provided.</summary>
    public bool HasSla { get; private set; }

    /// <summary>Includes dedicated onboarding/support team (SaaS tier only).</summary>
    public bool HasDedicatedTeam { get; private set; }

    // ── Computed Domain Property ───────────────────────────────────────────────

    /// <summary>
    /// True when MaxActiveProducts is null, meaning the plan has no product cap.
    /// This is a pure domain property — no corresponding DB column.
    /// </summary>
    public bool IsUnlimited => MaxActiveProducts == null;

    // ── EF Core Navigation ────────────────────────────────────────────────────

    /// <summary>Subscriptions currently on this plan.</summary>
    public IReadOnlyCollection<Subscription> Subscriptions { get; private set; } = [];

    /// <summary>Payment records associated with this plan.</summary>
    public IReadOnlyCollection<SubscriptionPayment> Payments { get; private set; } = [];

    // ── Private Constructor (EF Core) ─────────────────────────────────────────

    private SubscriptionPlan() { }

    // ── Factory Method ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new SubscriptionPlan. Used for admin-level plan management.
    /// For seeded plans, use this factory to ensure all invariants are set.
    /// </summary>
    public static SubscriptionPlan Create(
        string name,
        string tier,
        string billingCycle,
        decimal priceAmount,
        string currency,
        decimal commissionRate,
        int? maxActiveProducts,
        int? maxMonthlyTryOns,
        string supportLevel,
        bool isWhiteLabel = false,
        bool includesSourceCode = false,
        bool includesMobileApps = false,
        bool hasSla = false,
        bool hasDedicatedTeam = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessRuleException("PLAN_NAME_REQUIRED", "Subscription plan name is required.");

        if (priceAmount < 0)
            throw new BusinessRuleException("PLAN_PRICE_INVALID", "Plan price cannot be negative.");

        if (commissionRate is < 0 or > 1)
            throw new BusinessRuleException("PLAN_COMMISSION_INVALID", "Commission rate must be between 0 and 1.");

        return new SubscriptionPlan
        {
            Name = name.Trim(),
            Tier = tier,
            BillingCycle = billingCycle,
            PriceAmount = priceAmount,
            Currency = currency.ToUpperInvariant(),
            CommissionRate = commissionRate,
            MaxActiveProducts = maxActiveProducts,
            MaxMonthlyTryOns = maxMonthlyTryOns,
            SupportLevel = supportLevel,
            IsActive = true,
            IsWhiteLabel = isWhiteLabel,
            IncludesSourceCode = includesSourceCode,
            IncludesMobileApps = includesMobileApps,
            HasSla = hasSla,
            HasDedicatedTeam = hasDedicatedTeam
        };
    }

    // ── Mutation Methods ──────────────────────────────────────────────────────

    /// <summary>Deactivates the plan so it cannot be selected by new retailers.</summary>
    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }

    /// <summary>Re-activates a previously deactivated plan.</summary>
    public void Activate()
    {
        IsActive = true;
        SetUpdatedAudit(null, DateTime.UtcNow);
    }
}