namespace Application.Interfaces.External;

/// <summary>
/// Abstraction over Google ID token validation.
///
/// The Infrastructure implementation uses <c>GoogleJsonWebSignature.ValidateAsync</c>
/// from the <c>Google.Apis.Auth</c> NuGet package. This keeps Google's SDK out of
/// the Application layer so the Application remains infrastructure-agnostic.
/// </summary>
public interface IGoogleAuthService
{
    /// <summary>
    /// Validates a Google ID token issued by Google's Identity Platform and returns
    /// the authenticated user's profile data.
    /// </summary>
    /// <param name="idToken">The Google ID token string received from the client app.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="GoogleUserInfo"/> record with the validated user's details.</returns>
    /// <exception cref="ExternalServiceException">
    /// Thrown when the token is invalid, expired, the audience does not match,
    /// or Google's JWKS endpoint is unreachable.
    /// </exception>
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default);
}

/// <summary>
/// Parsed, validated payload extracted from a Google ID token.
/// All fields are sourced from verified Google token claims — never from client-provided data.
/// </summary>
/// <param name="GoogleId">
///   Unique Google subject identifier (sub claim).
///   This is the stable, permanent ID for the Google account.
/// </param>
/// <param name="Email">The user's verified email address (email claim).</param>
/// <param name="FullName">The user's display name (name claim from Google profile).</param>
/// <param name="IsEmailVerified">
///   Always <c>true</c> for successfully validated tokens —
///   Google does not issue ID tokens for unverified emails.
/// </param>
public sealed record GoogleUserInfo(
    string GoogleId,
    string Email,
    string FullName,
    bool IsEmailVerified);
