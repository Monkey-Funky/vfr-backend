namespace Application.Interfaces;


/// <summary>
/// Provides subscription-plan information for use in Application layer
/// business-rule enforcement (e.g. product limit checks).
///
/// IMPORTANT: This is a read-only service — it does NOT mutate subscription state.
/// All data is fetched from the database (NOT from cache) so that plan limits are
/// always evaluated against the most up-to-date plan configuration.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Returns the active subscription plan details for the given retailer.
    /// Returns null if the retailer has no active or trial subscription.
    /// </summary>
    /// <param name="retailerId">The retailer whose plan should be fetched.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CurrentPlanInfo?> GetCurrentPlanAsync(Guid retailerId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight read model carrying only the plan-limit fields
/// needed by business-rule checks. Not a full DTO.
/// </summary>
public sealed record CurrentPlanInfo(
    Guid PlanId,
    string PlanName,
    string Tier,
    int? MaxActiveProducts,
    int? MaxMonthlyTryOns
);
