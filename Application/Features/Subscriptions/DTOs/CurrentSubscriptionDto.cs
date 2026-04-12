using Domain.Enums.Subscription;

namespace Application.Features.Subscriptions.DTOs;

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
    bool CanUpgrade,
    bool CanDowngrade,
    bool CanCancel,
    bool CanStartTrial
);