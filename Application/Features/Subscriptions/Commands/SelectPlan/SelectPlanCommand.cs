namespace Application.Features.Subscriptions.Commands.SelectPlan;

/// <summary>
/// Selects a subscription plan and charges the retailer immediately.
/// Creates a SubscriptionPayment (Pending) then initiates a Stripe charge.
/// On success: creates or updates the Subscription to Active status.
/// The entire flow executes in a single DB transaction.
/// </summary>
public sealed record SelectPlanCommand(
    Guid PlanId,
    Guid PaymentMethodId,
    string BillingCycle
) : IRequest<Result<Guid>>;