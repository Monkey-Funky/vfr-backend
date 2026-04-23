using API.Controllers.BaseControllers;
using Application.Features.Customer.FitFeedback.Commands.SubmitFitFeedback;
using Application.Features.Customer.FitFeedback.DTOs;
using Application.Features.Customer.FitFeedback.Queries.GetFitFeedbackByOrder;
using Application.Features.Customer.FitFeedback.Queries.GetFitFeedbackByProduct;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[Route("api/customers/{customerId:guid}/fit-feedback")]
[SwaggerTag("Fit Feedback — Manage post-purchase sizing feedback")]
public sealed class FitFeedbackController : CustomerBaseApiController
{
    // ==============================================================
    // POST api/customers/{customerId}/fit-feedback
    // ==============================================================
    [HttpPost]
    [SwaggerOperation(
        Summary = "Submit fit feedback",
        Description = "Submits size accuracy feedback for a delivered order item.")]
    [ProducesResponseType(typeof(ApiResponse<FitFeedbackDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitFitFeedback(
        Guid customerId,
        [FromBody] SubmitFitFeedbackCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        var result = await Sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetFitFeedbackByOrder), new { customerId, orderId = result.OrderItemId }, new ApiResponse<FitFeedbackDto> { Data = result });
    }

    // ==============================================================
    // GET api/customers/{customerId}/fit-feedback/orders/{orderId}
    // ==============================================================
    [HttpGet("orders/{orderId:guid}")]
    [SwaggerOperation(
        Summary = "Get fit feedback by order",
        Description = "Retrieves fit feedback submitted for a specific order.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FitFeedbackDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFitFeedbackByOrder(
        Guid customerId,
        Guid orderId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerOwnership(customerId);
        var command = new GetFitFeedbackByOrderQuery(orderId, pageNumber, pageSize);
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/customers/{customerId}/fit-feedback/products/{productId}
    // ==============================================================
    [HttpGet("products/{productId:guid}")]
    [SwaggerOperation(
        Summary = "Get fit feedback by product",
        Description = "Retrieves fit feedback submitted for a specific product.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FitFeedbackDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFitFeedbackByProduct(
        Guid customerId,
        Guid productId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerOwnership(customerId);
        var command = new GetFitFeedbackByProductQuery(productId, pageNumber, pageSize);
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result);
    }
}
