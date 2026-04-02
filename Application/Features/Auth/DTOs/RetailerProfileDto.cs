namespace Application.Features.Auth.DTOs;

/// <summary>
/// Safe, public projection of a RetailerAccount.
/// Included in every AuthTokenResponse so the client has the profile
/// immediately after login without a separate API call.
///
/// SAFETY: PasswordHash, RefreshTokenHash, AccessFailedCount, LockoutEndAt
/// are intentionally excluded from this record.
/// </summary>
public sealed record RetailerProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string BrandName,
    string BusinessType,
    bool Has3DModels,
    string? BrandLogoUrl,
    string AccountStatus,
    bool IsEmailVerified
);