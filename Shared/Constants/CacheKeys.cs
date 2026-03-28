namespace Shared.Constants;

public static class CacheKeys
{
    // Subscription plans — invalidated on any plan update
    public static string SubscriptionPlans(string billingCycle)
        => $"subscription_plans:{billingCycle.ToLowerInvariant()}";

    // Per-retailer dashboard snapshot
    public static string DashboardSnapshot(Guid retailerId)
        => $"dashboard:{retailerId}";

    // Per-retailer product count (for plan limit checks)
    public static string ActiveProductCount(Guid retailerId)
        => $"product_count:{retailerId}";

    // Auth: refresh token version (for invalidation on logout)
    public static string RefreshTokenVersion(Guid retailerId)
        => $"rt_version:{retailerId}";
}