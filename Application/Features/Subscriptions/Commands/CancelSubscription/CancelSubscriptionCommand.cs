namespace Application.Features.Subscriptions.Commands.CancelSubscription;


/// <summary>
/// Cancels the authenticated retailer's active subscription.
/// Business rules enforced in the handler:
///   - Subscription must exist.
///   - Subscription must not already be Cancelled (SUBSCRIPTION_ALREADY_CANCELLED).
///   - Status transitions to Cancelled; EndDate set to now.
/// Returns HTTP 200 with the updated subscription status.
/// </summary>
public sealed record CancelSubscriptionCommand : IRequest<Result<bool>>;