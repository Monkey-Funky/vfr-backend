using Application.Interfaces.Services;

namespace API.Controllers.BaseControllers;

/// <summary>
/// Base controller for all authenticated Customer endpoints.
/// Provides tenant-scoped identity via CurrentCustomerId extracted from the JWT.
/// All controllers in the Customer module MUST inherit from this class.
/// </summary>
[ApiController]
[Authorize(Roles = "Customer")]
[Produces("application/json")]
public abstract class CustomerBaseApiController : CoreBaseApiController
{
    private ICurrentUserService? _currentUserService;
    /// <summary>
    /// The current authenticated customer's ID — ALWAYS sourced from the JWT claim.
    /// NEVER use a customerId from the route, query string, or request body.
    /// </summary>
    protected Guid CurrentCustomerId =>
        (_currentUserService ??= HttpContext.RequestServices
            .GetRequiredService<ICurrentUserService>())
        .CustomerId
        ?? throw new UnauthorizedException("Customer identity claim is missing.");

    /// <summary>
    /// IDOR protection guard. Call this in any action where the route contains
    /// a customer-scoped identifier that must match the JWT claim.
    /// Throws UnauthorizedException (→ HTTP 403) if the IDs do not match.
    /// </summary>
    protected void EnsureCustomerOwnership(Guid customerId)
    {
        if (customerId != CurrentCustomerId)
            throw new UnauthorizedException(
                "You do not have permission to access this resource.");
    }
}
