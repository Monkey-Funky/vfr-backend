namespace Application.Features.Settings.Commands.UpdateNotificationPreferences;

/// <summary>
/// Updates the notification preferences for the currently authenticated retailer.
/// PATCH semantics: only non-null fields are written. Null means "leave unchanged".
///
/// Example: sending <c>{ "EmailNotifications": false }</c> disables email
/// notifications without touching any other preference.
/// </summary>
public sealed record UpdateNotificationPreferencesCommand(
    bool? LowStockAlerts,
    bool? OrderStatusAlerts,
    bool? SubscriptionAlerts,
    bool? EmailNotifications,
    bool? InAppNotifications) : IRequest<Result<bool>>;