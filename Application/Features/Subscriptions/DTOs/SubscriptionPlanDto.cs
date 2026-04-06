using Domain.Entities.Subscriptions;

namespace Application.Features.Subscriptions.DTOs;

/// <summary>
/// Read model for a subscription plan.
/// Returned by GetAllSubscriptionPlansQuery and GetSubscriptionPlanByIdQuery.
/// </summary>
public sealed record SubscriptionPlanDto(
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
    bool HasDedicatedTeam,
    bool IsUnlimited
);

/// <summary>
/// Extension method to project a SubscriptionPlan entity to its DTO.
/// No AutoMapper — explicit mapping per 03-CodingStandards.md.
/// </summary>
public static class SubscriptionPlanMappingExtensions
{
    public static SubscriptionPlanDto ToDto(this SubscriptionPlan plan) =>
        new(
            plan.Id,
            plan.Name,
            plan.Tier,
            plan.BillingCycle,
            plan.PriceAmount,
            plan.Currency,
            plan.CommissionRate,
            plan.MaxActiveProducts,
            plan.MaxMonthlyTryOns,
            plan.SupportLevel,
            plan.IsActive,
            plan.IsWhiteLabel,
            plan.IncludesSourceCode,
            plan.IncludesMobileApps,
            plan.HasSla,
            plan.HasDedicatedTeam,
            plan.IsUnlimited
        );
}