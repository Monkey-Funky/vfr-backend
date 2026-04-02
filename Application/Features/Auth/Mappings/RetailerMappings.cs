using Application.Features.Auth.DTOs;

namespace Application.Features.Auth.Mappings;

/// <summary>
/// Manual mapping extension methods for the Auth feature.
///
/// AutoMapper is prohibited in this project.
/// All object-to-object conversions are expressed as explicit C# extension methods here.
/// These are called from command handlers — never from controllers or domain classes.
/// </summary>
public static class RetailerMappings
{
    /// <summary>
    /// Builds a complete <see cref="AuthTokenResponse"/> from a retailer entity and its tokens.
    /// Includes the profile snapshot via <see cref="ToProfileDto"/>.
    /// </summary>
    /// <param name="account">The authenticated retailer entity.</param>
    /// <param name="accessToken">The newly issued RS256 access JWT.</param>
    /// <param name="refreshToken">The newly issued raw refresh token (not hashed).</param>
    public static AuthTokenResponse ToAuthResponse(
        this RetailerAccount account,
        string accessToken,
        string refreshToken)
        => new(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: 15 * 60,               // 900 seconds = 15 minutes
            RetailerProfile: account.ToProfileDto()
        );

    /// <summary>
    /// Maps a <see cref="RetailerAccount"/> to a <see cref="RetailerProfileDto"/>.
    /// Excludes ALL sensitive fields: PasswordHash, RefreshTokenHash, AccessFailedCount,
    /// LockoutEndAt, GoogleId.
    /// </summary>
    public static RetailerProfileDto ToProfileDto(this RetailerAccount account)
        => new(
            Id: account.Id,
            FullName: account.FullName,
            Email: account.Email,
            BrandName: account.BrandName,
            BusinessType: account.BusinessType,
            Has3DModels: account.Has3DModels,
            BrandLogoUrl: account.BrandLogoUrl,
            AccountStatus: account.AccountStatus,
            IsEmailVerified: account.IsEmailVerified
        );
}