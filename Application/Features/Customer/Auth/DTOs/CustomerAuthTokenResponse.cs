
namespace Application.Features.Customer.Auth.DTOs;

/// <summary>
/// The successful authentication response body returned by:
///   • CustomerLoginCommand
///   • CustomerLoginGoogleCommand
///   • CustomerRegisterStep2Command
///   • CustomerRefreshTokenCommand
///
/// Never include raw passwords, hashed tokens, or AccessFailedCount in this record.
/// </summary>
public sealed record CustomerAuthTokenResponse(
    /// <summary>RS256-signed JWT access token. Valid for 15 minutes (900 seconds).</summary>
    string AccessToken,

    /// <summary>
    /// Opaque refresh token (raw, not hashed). Store securely on the client
    /// (HttpOnly cookie or secure storage). Valid for 7 days (30 with Remember Me).
    /// </summary>
    string RefreshToken,

    /// <summary>
    /// Access token lifetime in seconds. Always 900 (15 minutes).
    /// Clients should use this to schedule a proactive refresh.
    /// </summary>
    int ExpiresIn,

    /// <summary>A safe snapshot of the authenticated customer's profile.</summary>
    CustomerProfileDto CustomerProfile
);
