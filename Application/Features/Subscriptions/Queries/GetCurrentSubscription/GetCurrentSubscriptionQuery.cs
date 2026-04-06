using Application.Features.Subscriptions.DTOs;

namespace Application.Features.Subscriptions.Queries.GetCurrentSubscription;

/// <summary>
/// Returns the authenticated retailer's current subscription with plan details
/// and button state flags for the UI.
/// </summary>
public sealed record GetCurrentSubscriptionQuery : IRequest<CurrentSubscriptionDto>;