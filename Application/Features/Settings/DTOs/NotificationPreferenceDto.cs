

namespace Application.Features.Settings.DTOs;

/// <summary>
/// Read-only projection of a retailer's notification preference record.
/// Returned by <c>GetNotificationPreferencesQuery</c>.
/// </summary>
public sealed record NotificationPreferenceDto(
    bool LowStockAlerts,
    bool OrderStatusAlerts,
    bool SubscriptionAlerts,
    bool EmailNotifications,
    bool InAppNotifications);