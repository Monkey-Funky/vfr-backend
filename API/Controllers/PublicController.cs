using MediatR;

namespace API.Controllers;

/// <summary>
/// Base controller for unauthenticated endpoints — primarily the Auth module
/// (Login, Register, RefreshToken). Does NOT enforce [Authorize].
/// </summary>
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
public abstract class PublicController : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender =>
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected IActionResult OkResponse<T>(T data, string message = "Request successful")
        => Ok(ApiResponse<T>.SuccessResponse(data, message));
}