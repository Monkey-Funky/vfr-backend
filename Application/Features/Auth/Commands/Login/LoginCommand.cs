using Application.Features.Auth.DTOs;

namespace Application.Features.Auth.Commands.Login;

/// <summary>
/// Authenticates a retailer using email and password.
/// Returns an access token + refresh token pair on success.
///
/// RememberMe = true extends the refresh token TTL from 7 days to 30 days.
/// </summary>
public sealed record LoginCommand(
    string Email,
    string Password,
    bool RememberMe
) : IRequest<Result<AuthTokenResponse>>;
