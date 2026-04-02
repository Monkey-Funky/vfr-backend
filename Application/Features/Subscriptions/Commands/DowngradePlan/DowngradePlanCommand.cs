namespace Application.Features.Subscriptions.Commands.DowngradePlan;

/// <summary>
/// Schedules a plan downgrade to take effect at the next renewal date.
/// The current subscription remains Active until EndDate.
/// No charge is made at this point.
/// </summary>
public sealed record DowngradePlanCommand(
    Guid NewPlanId
) : IRequest<Result<bool>>;