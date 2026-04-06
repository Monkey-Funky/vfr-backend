using Domain.Enums.Subscription;

namespace Application.Features.Subscriptions.DTOs;

/// <summary>
/// Detailed subscription read model with full plan feature list.
/// Returned by GetCurrentSubscriptionDetailsQuery.
/// </summary>
public sealed record CurrentSubscriptionDetailsDto(
    Guid SubscriptionId,
    SubscriptionStatus Status,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? TrialEndsAt,
    bool IsRecurringEnabled,

    // ── Plan Features ──────────────────────────────────────────────────────────
    Guid PlanId,
    string PlanName,
    string Tier,
    string BillingCycle,
    decimal PriceAmount,
    string Currency,
    decimal CommissionRate,
    int? MaxActiveProducts,
    int? MaxMonthlyTryOns,
    string SupportLevel,
    bool IsUnlimited,

    // ── SaaS / Enterprise Flags ───────────────────────────────────────────────
    bool IsWhiteLabel,
    bool IncludesSourceCode,
    bool IncludesMobileApps,
    bool HasSla,
    bool HasDedicatedTeam,

    // ── Pending Downgrade Info ────────────────────────────────────────────────
    Guid? PendingDowngradePlanId,
    string? PendingDowngradePlanName,
    DateTime? PendingDowngradeEffectiveAt
);