using Domain.Enums.Subscription;

namespace Application.Features.Subscriptions.DTOs;

/// <summary>
/// Compact subscription read model.
/// Returned by StartTrialCommand on success.
/// </summary>
public sealed record SubscriptionSummaryDto(
    Guid SubscriptionId,
    Guid PlanId,
    string PlanName,
    string Tier,
    SubscriptionStatus Status,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? TrialEndsAt,
    bool IsRecurringEnabled,
    bool IsActive,
    bool IsInTrial
);