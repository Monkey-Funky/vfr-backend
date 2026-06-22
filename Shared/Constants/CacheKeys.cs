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

    // Per-retailer product detail (admin view)
    public static string ProductDetail(Guid retailerId, Guid productId)
        => $"product:{retailerId:N}:{productId:N}";

    // Retailer paginated product list prefix — use RemoveByPrefixAsync
    public static string ProductListPrefix(Guid retailerId)
        => $"products:{retailerId:N}:";

    // Per-retailer orders list prefix
    public static string OrderListPrefix(Guid retailerId)
        => $"orders:{retailerId:N}:";

    // Individual order
    public static string OrderDetail(Guid retailerId, Guid orderId)
        => $"order:{retailerId:N}:{orderId:N}";

    // Per-retailer inventory prefix
    public static string InventoryPrefix(Guid retailerId)
        => $"inventory:{retailerId:N}:";

    // Sub-categories for a parent category
    public static string SubCategories(Guid retailerId, Guid categoryId)
        => $"subcategories:{retailerId:N}:{categoryId:N}";

    // Payment methods list
    public static string PaymentMethods(Guid retailerId)
        => $"payment_methods:{retailerId:N}";

    // Current subscription
    public static string CurrentSubscription(Guid retailerId)
        => $"subscription:{retailerId:N}";

    // Unread notification count
    public static string UnreadNotificationCount(Guid retailerId)
        => $"notif_unread:{retailerId:N}";

    // Customer catalog browse (shared, no user id — public data)
    public static string ProductBrowse(string queryFingerprint)
        => $"catalog:browse:{queryFingerprint}";

    // Customer favorites list
    public static string CustomerFavoritesList(Guid customerId)
        => $"cust_favorites:{customerId:N}";

    // Customer favorites check (bulk) — prefix used for invalidation
    public static string CustomerFavoritesCheckPrefix(Guid customerId)
        => $"check_favorites:{customerId:N}";

    // Customer addresses
    public static string CustomerAddresses(Guid customerId)
        => $"cust_addresses:{customerId:N}";
}