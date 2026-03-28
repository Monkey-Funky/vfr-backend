namespace Domain.Exceptions;

/// <summary>
/// Thrown when an authenticated user attempts to access a resource they do not own.
/// This is the IDOR defence exception — maps to HTTP 403 Forbidden.
/// Note: HTTP 404 is returned instead of 403 in most IDOR cases (see BaseApiController)
/// to avoid confirming another tenant's resource existence.
/// </summary>
public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "Access denied.")
        : base(message) { }
}