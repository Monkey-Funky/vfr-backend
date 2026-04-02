namespace Application.Features.Auth.Commands.Logout;

/// <summary>
/// Logs the currently authenticated retailer out by invalidating their refresh token.
///
/// The RetailerId is extracted from the JWT by ICurrentUserService — it is NOT
/// passed as a command parameter. This enforces the IDOR security boundary.
/// </summary>
public sealed record LogoutCommand() : IRequest<Result<bool>>;