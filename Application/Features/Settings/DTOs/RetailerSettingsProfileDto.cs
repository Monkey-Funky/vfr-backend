

namespace Application.Features.Settings.DTOs;

/// <summary>
/// Full profile projection returned by <c>GetRetailerProfileQuery</c>.
/// Excludes all sensitive fields: PasswordHash, RefreshTokenHash, AccessFailedCount.
/// </summary>
public sealed record RetailerSettingsProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string BrandName,
    string BusinessType,
    bool Has3DModels,
    string? AvatarUrl,
    string? BrandLogoUrl,
    bool IsEmailVerified,
    string AccountStatus,
    Guid? SubscriptionId,
    decimal AvailableBalance,
    DateTime CreatedAt,
    DateTime? UpdatedAt);   