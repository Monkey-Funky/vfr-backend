using Amazon.Runtime.Internal;
using Application.Features.Orders.Commands.UpdateOrderStatus;
using Application.Features.Orders.DTOs;
using Application.Features.Orders.Queries.ExportOrdersCsv;
using Application.Features.Orders.Queries.GetOrderById;
using Application.Features.Orders.Queries.GetOrders;
using CsvHelper;
using CsvHelper.Configuration;
using Swashbuckle.AspNetCore.Annotations;
using System.Formats.Asn1;
using System.Globalization;

namespace API.Controllers.Orders;

/// <summary>
/// Manages orders for the authenticated retailer.
/// All endpoints are JWT-protected (Retailer role).
/// RetailerId is ALWAYS sourced from the JWT (CurrentRetailerId) — never from request body.
/// </summary>
[SwaggerTag("Orders — view and manage customer orders")]
[Route("api/retailers/{retailerId:guid}/orders")]
public sealed class OrdersController : BaseApiController
{
    // =========================================================================
    // GET /api/retailers/{retailerId}/orders
    // =========================================================================

    /// <summary>Returns a paginated, filtered list of orders for the retailer.</summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List orders",
        Description = "Returns paginated orders. Supports status filter and customer name / order ID search.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OrderDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetOrders(
        [FromRoute] Guid retailerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        // IDOR guard — retailerId in route must match the JWT
        EnsureRetailerOwnership(retailerId);

        var query = new GetOrdersQuery(pageNumber, pageSize, status, searchTerm);
        var result = await Sender.Send(query, cancellationToken);

        return OkResponse(result);
    }

    // =========================================================================
    // GET /api/retailers/{retailerId}/orders/{orderId}
    // =========================================================================

    /// <summary>Returns a single order with all its line items.</summary>
    [HttpGet("{orderId:guid}", Name = "GetOrderById")]
    [SwaggerOperation(
        Summary = "Get order by ID",
        Description = "Returns the full order detail including all items. " +
                      "Returns 404 if the order does not exist or belongs to another retailer.")]
    [ProducesResponseType(typeof(ApiResponse<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrderById(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var query = new GetOrderByIdQuery(orderId);
        var result = await Sender.Send(query, cancellationToken);

        return OkResponse(result);
    }

    // =========================================================================
    // PATCH /api/retailers/{retailerId}/orders/{orderId}/status
    // =========================================================================

    /// <summary>Transitions an order to a new status.</summary>
    [HttpPatch("{orderId:guid}/status")]
    [SwaggerOperation(
        Summary = "Update order status",
        Description = "Transitions the order status. Valid transitions: " +
                      "NotProcessed→Processing/Cancelled, Processing→Shipped/Cancelled, " +
                      "Shipped→Delivered. Delivered and Cancelled are terminal states. " +
                      "Returns 422 for invalid transitions. Returns 404 if order not found or not owned.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateOrderStatus(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var command = new UpdateOrderStatusCommand(
            OrderId: orderId,
            RetailerId: CurrentRetailerId, // ✅ ALWAYS from JWT — never from request body
            NewStatus: request.NewStatus);

        var result = await Sender.Send(command, cancellationToken);

        // ✅ FIX: Result<bool> uses .Data not .Value (CS1061 resolved)
        // For a 204 NoContent response we don't need to read result.Data at all —
        // success is determined by result.IsSuccess.
        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.FailureResponse(result.Message, result.Errors));

        return NoContent(); // 204 — status updated successfully
    }

    // =========================================================================
    // GET /api/retailers/{retailerId}/orders/export/csv
    // =========================================================================

    /// <summary>Exports all orders as a CSV file download.</summary>
    [HttpGet("export/csv")]
    [SwaggerOperation(
        Summary = "Export orders as CSV",
        Description = "Streams all orders for the retailer as a UTF-8 CSV file. " +
                      "Columns: OrderId, CustomerName, OrderDate, TotalAmount, Currency, Status, ItemCount, CreatedAt.")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var query = new ExportOrdersCsvQuery();

        // ✅ FIX: Pass HttpContext.RequestAborted (NOT cancellationToken directly) so that
        // when the client disconnects mid-download the EF Core DataReader is disposed
        // and the PostgreSQL connection is returned to the pool immediately.
        var csvBytes = await Sender.Send(query, HttpContext.RequestAborted);

        var fileName = $"orders_{retailerId:N}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(csvBytes, "text/csv", fileName);
    }
}

// =========================================================================
// Request contract
// =========================================================================

/// <summary>Request body for PATCH /orders/{orderId}/status.</summary>
public sealed record UpdateOrderStatusRequest(string NewStatus);