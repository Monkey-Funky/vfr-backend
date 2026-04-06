using Application.Features.Subscriptions.DTOs;

namespace Application.Features.Subscriptions.Commands.StartTrial;

/// <summary>
/// Starts a 14-day free trial for a retailer with no existing subscription.
/// Validation:
///   - Retailer must have no existing Subscription record (Status = None).
///   - Retailer account must exist and be authenticated.
/// </summary>
public sealed record StartTrialCommand : IRequest<Result<SubscriptionSummaryDto>>;