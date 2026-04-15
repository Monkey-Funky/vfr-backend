using Application.Interfaces.Services;

namespace API.Controllers.BaseControllers;

/// <summary>
/// Base controller for all authenticated Retailer endpoints.
/// Provides tenant-scoped identity via CurrentRetailerId extracted from the JWT.
/// All controllers in the Retailer module MUST inherit from this class.
/// </summary>
[ApiController]
[Authorize(Roles = Domain.Constants.Roles.Retailer)]
[Produces("application/json")]
public abstract class RetailerBaseApiController : CoreBaseApiController
{
    private ICurrentUserService? _currentUserService;
    /// <summary>
    /// The current authenticated retailer's ID — ALWAYS sourced from the JWT claim.
    /// NEVER use a retailerId from the route, query string, or request body.
    /// </summary>
    protected Guid CurrentRetailerId =>
        (_currentUserService ??= HttpContext.RequestServices
            .GetRequiredService<ICurrentUserService>())
        .RetailerId
        ?? throw new UnauthorizedException("Retailer identity claim is missing.");

    /// <summary>
    /// IDOR protection guard. Call this in any action where the route contains
    /// a retailer-scoped identifier that must match the JWT claim.
    /// Throws UnauthorizedException (→ HTTP 403) if the IDs do not match.
    /// </summary>
    protected void EnsureRetailerOwnership(Guid retailerId)
    {
        if (retailerId != CurrentRetailerId)
            throw new UnauthorizedException(
                "You do not have permission to access this resource.");
    }
}