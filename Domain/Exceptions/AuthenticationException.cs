namespace Domain.Exceptions;

/// <summary>
/// Thrown when an authentication attempt fails — e.g. invalid credentials,
/// an invalid or expired token, or a missing identity claim on a public auth endpoint.
///
/// Maps to HTTP 401 Unauthorized.
///
/// Design intent
/// ─────────────
/// This exception is distinct from <see cref="UnauthorizedException"/>, which maps to
/// HTTP 403 Forbidden and is reserved for IDOR / access-control violations on
/// resources the caller is not permitted to touch.
///
/// Rule of thumb:
///   • 401 (AuthenticationException)  → "I don't know who you are."
///   • 403 (UnauthorizedException)    → "I know who you are, but you can't do that."
///
/// Usage
/// ─────
/// Throw this from any auth command handler when credentials or tokens fail validation:
///   throw new AuthenticationException("Invalid email or password.");
///   throw new AuthenticationException("The access token is invalid.");
/// </summary>
public sealed class AuthenticationException : DomainException
{
    public AuthenticationException(string message = "Authentication failed.")
        : base(message) { }
}