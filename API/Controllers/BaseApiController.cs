using Application.Interfaces.Services;
using MediatR;

namespace API.Controllers;

/// <summary>
/// Base controller for all authenticated Retailer endpoints.
/// Provides tenant-scoped identity via CurrentRetailerId extracted from the JWT.
/// All controllers in the Retailer module MUST inherit from this class.
/// </summary>
[ApiController]
[Authorize(Roles = "Retailer")]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    private ISender? _sender;
    private ICurrentUserService? _currentUserService;

    /// <summary>
    /// MediatR sender — injected lazily from the service container.
    /// </summary>
    protected ISender Sender =>
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

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
    ///
    /// Example usage:
    ///   [HttpGet("{retailerId}/products")]
    ///   public async Task&lt;IActionResult&gt; GetProducts(Guid retailerId)
    ///   {
    ///       EnsureRetailerOwnership(retailerId);
    ///       ...
    ///   }
    /// </summary>
    protected void EnsureRetailerOwnership(Guid retailerId)
    {
        if (retailerId != CurrentRetailerId)
            throw new UnauthorizedException(
                "You do not have permission to access this resource.");
    }

    // ── Standardised response helpers ────────────────────────────────────────

    protected IActionResult OkResponse<T>(T data, string message = "Request successful")
        => Ok(ApiResponse<T>.SuccessResponse(data, message));

    protected IActionResult CreatedResponse<T>(string routeName, object routeValues, T data)
        => CreatedAtRoute(routeName, routeValues,
            ApiResponse<T>.SuccessResponse(data, "Resource created successfully."));

    protected IActionResult NoContentResponse()
        => NoContent();

    protected IActionResult NotFoundResponse(string message)
        => NotFound(new ApiErrorResponse
        {
            Code = "NOT_FOUND",
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
}