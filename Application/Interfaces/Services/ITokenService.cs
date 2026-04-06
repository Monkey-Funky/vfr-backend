namespace Application.Interfaces.Services;

/// <summary>
/// Abstraction over JWT RS256 token generation and validation.
/// The implementation (TokenService) lives in Infrastructure.Services and is
/// registered in P-012.
///
/// ACCESS TOKEN FORMAT (RS256 JWT):
///   Claims: sub (RetailerId), email, role ("Retailer"), brand_name, jti (Guid)
///   TTL   : 15 minutes
///
/// REFRESH TOKEN FORMAT:
///   Opaque random bytes (not a JWT). Returned raw to the client.
///   Stored as BCrypt hash in RetailerAccount.RefreshTokenHash.
///
/// STEP TOKEN FORMAT (RS256 JWT, short-lived):
///   Claims: step (int), temp_account_id (Guid)
///   TTL   : 15 minutes
///   Used only between Step 1 and Step 2 of registration.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a signed RS256 access JWT for the given retailer.
    /// </summary>
    /// <param name="account">The authenticated retailer. Must not be null.</param>
    /// <returns>Signed JWT string ready to be returned to the client.</returns>
    string GenerateAccessToken(RetailerAccount account);

    /// <summary>
    /// Generates a cryptographically random opaque refresh token.
    /// The raw token is returned to the client.
    /// The CALLER must hash it (BCrypt) before persisting it to the database.
    /// </summary>
    /// <returns>Raw refresh token string (URL-safe Base64).</returns>
    string GenerateRefreshToken();

    /// <summary>
    /// Generates a short-lived RS256 JWT used as a "step token" between
    /// Step 1 and Step 2 of registration.
    /// </summary>
    /// <param name="tempId">The ID of the partially created RetailerAccount.</param>
    /// <param name="step">The registration step number (always 1 for Step 1).</param>
    /// <returns>Signed step-token JWT string.</returns>
    string GenerateTempStepToken(Guid tempId, int step);

    /// <summary>
    /// Validates a step token and returns its ClaimsPrincipal if valid.
    /// Returns <c>null</c> if the token is expired, tampered, or structurally invalid.
    /// </summary>
    /// <param name="token">The step token received from the client in Step 2.</param>
    ClaimsPrincipal? ValidateTempStepToken(string token);

    /// <summary>
    /// Extracts the ClaimsPrincipal from an expired access token WITHOUT validating its lifetime.
    /// Used by the RefreshToken handler to retrieve the RetailerId from the old token.
    ///
    /// All other validation rules (issuer, audience, signature) still apply.
    /// Returns <c>null</c> if the token is structurally invalid or the signature is bad.
    /// </summary>
    /// <param name="accessToken">The expired (or near-expired) access JWT from the client.</param>
    ClaimsPrincipal? GetClaimsFromExpiredToken(string accessToken);
}
