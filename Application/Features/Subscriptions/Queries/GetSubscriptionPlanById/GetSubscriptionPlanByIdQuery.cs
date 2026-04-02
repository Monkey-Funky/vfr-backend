
namespace Application.Features.Subscriptions.Queries.GetSubscriptionPlanById;

/// <summary>
/// Returns a single subscription plan by its ID.
/// </summary>
public sealed record GetSubscriptionPlanByIdQuery(
    Guid PlanId
) : IRequest<SubscriptionPlanDto>;