using Application.Features.Subscriptions.DTOs;

namespace Application.Features.Subscriptions.Queries.GetCurrentSubscriptionDetails;

/// <summary>
/// Returns the authenticated retailer's current subscription with the full
/// plan feature list (commission rate, limits, SaaS flags, etc.).
/// Used by the account/billing detail page.
/// </summary>
public sealed record GetCurrentSubscriptionDetailsQuery : IRequest<CurrentSubscriptionDetailsDto>;