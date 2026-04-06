using Application.Features.Subscriptions.DTOs;

namespace Application.Features.Subscriptions.Queries.GetAllSubscriptionPlans;

/// <summary>
/// Returns all active subscription plans, grouped by tier.
/// Results are cached in Redis (TTL 1 hour) per billing cycle.
/// This is a public query — no retailer authentication required.
/// </summary>
/// <param name="BillingCycle">
///     Optional filter: "Monthly", "Yearly", "SaaS", or null for all.
/// </param>
public sealed record GetAllSubscriptionPlansQuery(
    string? BillingCycle = null
) : IRequest<IReadOnlyList<SubscriptionPlanGroupDto>>;