namespace Application.Features.Subscriptions.Commands.UpgradePlan;

/// <summary>
/// Immediately upgrades the retailer's subscription to a higher-tier plan.
/// A prorated charge is calculated for the remaining days in the current billing cycle
/// and charged via Stripe. The plan change takes effect immediately.
/// </summary>
public sealed record UpgradePlanCommand(
    Guid NewPlanId,
    Guid PaymentMethodId
) : IRequest<Result<bool>>;