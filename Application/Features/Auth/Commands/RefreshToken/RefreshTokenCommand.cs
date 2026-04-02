using Application.Features.Auth.DTOs;

namespace Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Rotates the refresh token and issues a new access + refresh token pair.
///
/// Token rotation: the old refresh token is immediately invalidated and a
/// new one is issued. The client must update its stored refresh token.
///
/// The AccessToken may be expired — the handler extracts the RetailerId from
/// its claims without validating the expiry signature.
/// </summary>
public sealed record RefreshTokenCommand(
    /// <summary>
    /// The expired (or soon-to-expire) access JWT.
    /// Used only to extract the RetailerId claim — lifetime is NOT validated here.
    /// </summary>
    string AccessToken,

    /// <summary>
    /// The raw refresh token stored by the client.
    /// Verified against the hashed version in the DB.
    /// </summary>
    string RefreshToken
) : IRequest<Result<AuthTokenResponse>>;