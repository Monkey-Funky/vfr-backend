using Application.Features.Settings.DTOs;

namespace Application.Features.Settings.Mappings;

/// <summary>
/// Manual mapping extension methods for the Settings feature.
/// No AutoMapper — all mappings are explicit, compile-time safe, and zero-reflection.
/// </summary>
public static class SettingsMappings
{
    /// <summary>
    /// Projects a <see cref="RetailerAccount"/> to a <see cref="RetailerSettingsProfileDto"/>.
    /// Never includes PasswordHash, RefreshTokenHash, or AccessFailedCount.
    /// </summary>
    public static RetailerSettingsProfileDto ToSettingsProfileDto(this RetailerAccount account)
    {
        return new RetailerSettingsProfileDto(
            Id: account.Id,
            FullName: account.FullName,
            Email: account.Email,
            PhoneNumber: account.PhoneNumber,
            BrandName: account.BrandName,
            BusinessType: account.BusinessType,
            Has3DModels: account.Has3DModels,
            AvatarUrl: account.AvatarUrl,
            BrandLogoUrl: account.BrandLogoUrl,
            IsEmailVerified: account.IsEmailVerified,
            AccountStatus: account.AccountStatus,
            SubscriptionId: account.SubscriptionId,
            AvailableBalance: account.AvailableBalance,
            CreatedAt: account.CreatedAt,
            UpdatedAt: account.UpdatedAt);
    }

    /// <summary>
    /// Projects a <see cref="NotificationPreference"/> to a <see cref="NotificationPreferenceDto"/>.
    /// </summary>
    public static NotificationPreferenceDto ToDto(this NotificationPreference preference)
    {
        return new NotificationPreferenceDto(
            LowStockAlerts: preference.LowStockAlerts,
            OrderStatusAlerts: preference.OrderStatusAlerts,
            SubscriptionAlerts: preference.SubscriptionAlerts,
            EmailNotifications: preference.EmailNotifications,
            InAppNotifications: preference.InAppNotifications);
    }
}