using Application.Interfaces.Services;
using MediatR;

namespace API.Controllers.BaseControllers;
/// <summary>
/// Base controller for all authenticated endpoints.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class CoreBaseApiController : ControllerBase
{
    private ISender? _sender;
    protected ISender Sender => 
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    // ── Standardised response helpers ────────────────────────────────────────
    protected IActionResult OkResponse<T>(T data, string message = "Request successful")
        => Ok(ApiResponse<T>.SuccessResponse(data, message));

    protected IActionResult CreatedResponse<T>(string routeName, object routeValues, T data)
        => CreatedAtRoute(routeName, routeValues, ApiResponse<T>.SuccessResponse(data, "Resource created successfully."));

    protected IActionResult NoContentResponse() => NoContent();

    protected IActionResult NotFoundResponse(string message)
        => NotFound(new ApiErrorResponse
        {
            Code = "NOT_FOUND",
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
}