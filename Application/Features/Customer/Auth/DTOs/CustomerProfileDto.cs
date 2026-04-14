namespace Application.Features.Customer.Auth.DTOs;

/// <summary>
/// Safe, public projection of a CustomerAccount.
/// Included in every CustomerAuthTokenResponse so the client has the profile
/// immediately after login without a separate API call.
///
/// SAFETY: PasswordHash, RefreshTokenHash, AccessFailedCount, LockoutEndAt
/// are intentionally excluded from this record.
/// </summary>
public sealed record CustomerProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? Gender,
    string? AvatarUrl,
    string Status);
