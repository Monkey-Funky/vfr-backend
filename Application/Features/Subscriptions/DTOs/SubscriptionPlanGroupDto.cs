namespace Application.Features.Subscriptions.DTOs;

/// <summary>
/// Represents a tier group of subscription plans.
/// Returned by GetAllSubscriptionPlansQuery (plans grouped by Tier).
/// </summary>
public sealed record SubscriptionPlanGroupDto(
    string Tier,
    IReadOnlyList<SubscriptionPlanDto> Plans
);