namespace Application.Features.Subscriptions.DTOs;

/// <summary>
/// Read model for a subscription plan — retailer-facing pricing page.
/// CommissionRate is intentionally excluded: it is internal business data
/// and must never be exposed to retailers via any API response.
/// </summary>
public sealed record SubscriptionPlanDto(
    Guid Id,
    string Name,
    string Tier,
    string BillingCycle,
    decimal PriceAmount,
    string Currency,
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
/// Extension method to project a SubscriptionPlan entity to its retailer-facing DTO.
/// CommissionRate is deliberately excluded from the mapping — it is internal data.
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