namespace Application.Interfaces;
/// <summary>
/// Provides identity information for the authenticated retailer extracted
/// from the validated RS256 JWT token. RetailerId is ALWAYS sourced from
/// this service — NEVER from request body or URL path parameters.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// The authenticated retailer's ID — extracted from the JWT 'sub' claim.
    /// Null if the request is unauthenticated.
    /// </summary>
    Guid? RetailerId { get; }

    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IEnumerable<string> Roles { get; }

    bool IsInRole(string role);
}