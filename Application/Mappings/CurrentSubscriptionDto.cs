using Domain.Enums;

namespace Application.Mappings;

/// <summary>
/// Full current-subscription read model including button state.
/// Returned by GetCurrentSubscriptionQuery.
/// </summary>
public sealed record CurrentSubscriptionDto(
    Guid SubscriptionId,
    SubscriptionStatus Status,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? TrialEndsAt,
    bool IsRecurringEnabled,
    bool IsActive,
    bool IsInTrial,
    SubscriptionPlanDto CurrentPlan,
    SubscriptionPlanDto? PendingDowngradePlan,

    // ── UI Button State ────────────────────────────────────────────────────────
    // The frontend uses these flags to show/hide action buttons, avoiding
    // business-rule duplication in the client.

    /// <summary>True when an upgrade action button should be shown.</summary>
    bool CanUpgrade,

    /// <summary>True when a downgrade action button should be shown.</summary>
    bool CanDowngrade,

    /// <summary>True when a cancel subscription button should be shown.</summary>
    bool CanCancel,

    /// <summary>True when a start-trial button should be shown.</summary>
    bool CanStartTrial
);