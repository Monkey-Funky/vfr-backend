
using Application.Interfaces.External;
using Google.Apis.Auth;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Auth;

/// <summary>
/// Validates Google ID tokens using <see cref="GoogleJsonWebSignature.ValidateAsync"/>.
///
/// Google's library verifies the token signature against Google's published JWKS,
/// checks the token's expiry, and validates the audience (ClientId).
///
/// CONFIGURATION: Requires "Google:ClientId" to be set in configuration / secrets.
/// The ClientId is the OAuth2 client ID registered in Google Cloud Console for the app.
/// </summary>
public sealed class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleSettings _settings;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(
        IOptions<GoogleSettings> settings,
        ILogger<GoogleAuthService> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(_settings.ClientId))
            throw new InvalidOperationException(
                "Google:ClientId is not configured. " +
                "Set it via user-secrets or environment variable.");
    }

    /// <inheritdoc />
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new ExternalServiceException("GOOGLE_AUTH_FAILED", "Google ID token must not be empty.");

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [_settings.ClientId],
                });

            _logger.LogInformation(
                "GoogleAuthService — token validated successfully. GoogleId: {GoogleId}, Email: {Email}",
                payload.Subject, payload.Email);

            return new GoogleUserInfo(
                GoogleId: payload.Subject,
                Email: payload.Email,
                FullName: payload.Name ?? string.Empty,
                IsEmailVerified: payload.EmailVerified);
        }
        catch (InvalidJwtException ex)
        {
            _logger.LogWarning(ex, "GoogleAuthService — invalid Google ID token.");
            throw new ExternalServiceException("GOOGLE_AUTH_FAILED",
                "The Google ID token is invalid or has expired. Please sign in with Google again.");
        }
        catch (Exception ex) when (ex is not ExternalServiceException)
        {
            _logger.LogError(ex, "GoogleAuthService — unexpected error during Google token validation.");
            throw new ExternalServiceException("GOOGLE_AUTH_FAILED",
                "Google authentication is temporarily unavailable. Please try again.");
        }
    }
}