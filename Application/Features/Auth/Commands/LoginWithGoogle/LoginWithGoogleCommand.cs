using Application.Features.Auth.DTOs;

namespace Application.Features.Auth.Commands.LoginWithGoogle;

/// <summary>
/// Authenticates a retailer using a Google ID token obtained from the client-side
/// Google Sign-In SDK. Creates the account if it does not already exist.
/// </summary>
public sealed record LoginWithGoogleCommand(
    /// <summary>
    /// The Google ID token (JWT) from the client-side Google Sign-In SDK.
    /// This is validated server-side via IGoogleAuthService.
    /// </summary>
    string GoogleIdToken
) : IRequest<Result<AuthTokenResponse>>;