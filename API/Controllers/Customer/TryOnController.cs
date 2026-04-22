using API.Controllers.BaseControllers;
using Application.Features.Customer.VirtualTryOn.Commands.InitiateTryOn;
using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionById;
using Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessions;
using Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionsByProduct;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[Route("api/customers/{customerId:guid}")]
[SwaggerTag("Virtual Try-On — Execute ML try-ons and query session history")]
public sealed class TryOnController : CustomerBaseApiController
{
    // ==============================================================
    // POST api/customers/{customerId}/try-on
    // ==============================================================
    [HttpPost("try-on")]
    [EnableRateLimiting("customer-tryon")]
    [SwaggerOperation(
        Summary = "Initiate a virtual try-on session",
        Description = "Invokes ML services for try-on modeling. Secured with strict concurrent rate limiting.")]
    [ProducesResponseType(typeof(ApiResponse<TryOnResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> InitiateTryOn(
        Guid customerId,
        [FromBody] InitiateTryOnCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/customers/{customerId}/try-on/sessions
    // ==============================================================
    [HttpGet("try-on/sessions")]
    [SwaggerOperation(
        Summary = "Get try-on session history",
        Description = "Retrieves paginated history of all virtual try-ons for the customer (descending).")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VirtualTryOnSessionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTryOnSessions(
        Guid customerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerOwnership(customerId);
        var command = new GetTryOnSessionsQuery(customerId, pageNumber, pageSize);
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/customers/{customerId}/try-on/sessions/{sessionId}
    // ==============================================================
    [HttpGet("try-on/sessions/{sessionId:guid}")]
    [SwaggerOperation(
        Summary = "Get try-on session details",
        Description = "Fetches a specific session (useful for UI polling of asynchronous processing states).")]
    [ProducesResponseType(typeof(ApiResponse<VirtualTryOnSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTryOnSession(
        Guid customerId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        var result = await Sender.Send(new GetTryOnSessionByIdQuery(sessionId), cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/customers/{customerId}/products/{productId}/sessions
    // ==============================================================
    [HttpGet("products/{productId:guid}/sessions")]
    [SwaggerOperation(
        Summary = "Get item-specific try-on sessions",
        Description = "Retrieves all try-on attempts for a specific product for the calling customer.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VirtualTryOnSessionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTryOnSessionsByProduct(
        Guid customerId,
        Guid productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerOwnership(customerId);
        var command = new GetTryOnSessionsByProductQuery(productId, pageNumber, pageSize);
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }
}
