

namespace Application.Features.Settings.DTOs;

/// <summary>
/// Full profile projection returned by GetRetailerProfileQuery.
/// Excludes all sensitive fields: PasswordHash, RefreshTokenHash,
/// RefreshTokenExpiresAt, GoogleId, AccessFailedCount.
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