using Application.Interfaces.Services;
using MediatR;

namespace API.Controllers.BaseControllers;

/// <summary>
/// Base controller for all authenticated endpoints.
///
/// Response helpers:
///   OkResponse&lt;T&gt;(T data)          — wraps a plain value in ApiResponse&lt;T&gt;.
///   OkResponse&lt;T&gt;(Result&lt;T&gt; result) — unwraps a Result&lt;T&gt; into ApiResponse&lt;T&gt;
///                                       so the caller always receives the actual
///                                       data type, never a nested Result object.
///
/// Root-Cause Fix (RC5 & RC6):
///   Previously, controllers that called OkResponse(result) where result was
///   Result&lt;T&gt; produced ApiResponse&lt;Result&lt;T&gt;&gt;. Consumers deserialising as
///   ApiResponse&lt;T&gt; therefore received null / default values for every field
///   because the inner Result object did not match the target DTO constructor.
///   The new overload resolves this: C# overload resolution prefers the more
///   specific Result&lt;T&gt; signature, so OkResponse(result) now always yields
///   ApiResponse&lt;T&gt; with the correct data, message, and error collection.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class CoreBaseApiController : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender =>
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    // ── Standardised response helpers ────────────────────────────────────────

    /// <summary>
    /// Wraps a plain value in a successful <see cref="ApiResponse{T}"/>.
    /// Used by handlers that return <typeparamref name="T"/> directly
    /// (e.g. <c>IRequest&lt;PagedResult&lt;ProductCardDto&gt;&gt;</c>).
    /// </summary>
    protected IActionResult OkResponse<T>(T data, string message = "Request successful")
        => Ok(ApiResponse<T>.SuccessResponse(data, message));

    /// <summary>
    /// Unwraps a <see cref="Result{T}"/> into a proper <see cref="ApiResponse{T}"/>.
    /// C# overload resolution favours this signature over
    /// <see cref="OkResponse{T}(T, string)"/> whenever the argument is
    /// <c>Result&lt;T&gt;</c>, which prevents the caller from accidentally
    /// receiving <c>ApiResponse&lt;Result&lt;T&gt;&gt;</c>.
    /// </summary>
    protected IActionResult OkResponse<T>(Result<T> result)
        => Ok(ApiResponse<T>.FromResult(result));

    protected IActionResult CreatedResponse<T>(string routeName, object routeValues, T data)
        => CreatedAtRoute(routeName, routeValues,
            ApiResponse<T>.SuccessResponse(data, "Resource created successfully."));

    protected IActionResult NoContentResponse() => NoContent();

    protected IActionResult NotFoundResponse(string message)
        => NotFound(new ApiErrorResponse
        {
            Code = "NOT_FOUND",
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
}