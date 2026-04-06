using Application.Features.Inventory.Commands.AdjustStock;
using Application.Features.Inventory.Commands.DeleteInventoryRecord;
using Application.Features.Inventory.DTOs;
using Application.Features.Inventory.Queries.ExportInventoryCsv;
using Application.Features.Inventory.Queries.GetInventory;
using Application.Features.Inventory.Queries.GetInventoryByProductId;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Inventory;

/// <summary>
/// Manages inventory records for the authenticated retailer.
/// All endpoints are JWT-protected (Retailer role).
/// RetailerId is ALWAYS sourced from the JWT (CurrentRetailerId) — never from the request body.
///
/// Route: api/retailers/{retailerId:guid}/inventory
/// </summary>
[SwaggerTag("Inventory — manage product stock levels and adjustments")]
[Route("api/retailers/{retailerId:guid}/inventory")]
public sealed class InventoryController : BaseApiController
{
    // =========================================================================
    // GET /api/retailers/{retailerId}/inventory
    // =========================================================================

    /// <summary>Returns a paginated list of inventory records for the retailer.</summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List inventory records",
        Description = "Returns a paginated list of inventory records. " +
                      "Supports filtering by product name (partial, case-insensitive). " +
                      "Supports sorting by sold quantity descending (best-sellers first).")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InventoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetInventory(
        [FromRoute] Guid retailerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? productName = null,
        [FromQuery] bool sortBySoldQuantityDesc = false,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var query = new GetInventoryQuery(pageNumber, pageSize, productName, sortBySoldQuantityDesc);
        var result = await Sender.Send(query, cancellationToken);

        return OkResponse(result);
    }

    // =========================================================================
    // GET /api/retailers/{retailerId}/inventory/product/{productId}
    // =========================================================================

    /// <summary>Returns the inventory record for a specific product.</summary>
    [HttpGet("product/{productId:guid}", Name = "GetInventoryByProductId")]
    [SwaggerOperation(
        Summary = "Get inventory by product ID",
        Description = "Returns the inventory record linked to the given product. " +
                      "Returns 404 if no inventory record exists or if the product " +
                      "does not belong to the authenticated retailer.")]
    [ProducesResponseType(typeof(ApiResponse<InventoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInventoryByProductId(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid productId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var query = new GetInventoryByProductIdQuery(productId);
        var result = await Sender.Send(query, cancellationToken);

        return OkResponse(result);
    }

    // =========================================================================
    // PATCH /api/retailers/{retailerId}/inventory/{inventoryRecordId}/adjust
    // =========================================================================

    /// <summary>Adjusts the stock quantity of an inventory record.</summary>
    [HttpPatch("{inventoryRecordId:guid}/adjust")]
    [SwaggerOperation(
        Summary = "Adjust stock quantity",
        Description = "Sets the inventory stock to the provided absolute NewQuantity value. " +
                      "NewQuantity must be >= 0 (STOCK_FLOOR_VIOLATION is returned otherwise). " +
                      "Reason is required for ManualIncrease and ManualDecrease adjustment types. " +
                      "Creates an immutable StockAdjustment audit record in the same transaction. " +
                      "Raises a LowStockWarning notification if stock drops to or below the threshold. " +
                      "Returns 422 on concurrent update conflict after one retry.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AdjustStock(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid inventoryRecordId,
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var command = new AdjustStockCommand(
            InventoryRecordId: inventoryRecordId,
            NewQuantity: request.NewQuantity,
            Type: request.Type,
            Reason: request.Reason);

        var result = await Sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.FailureResponse(result.Message, result.Errors));

        return NoContent();
    }

    // =========================================================================
    // DELETE /api/retailers/{retailerId}/inventory/{inventoryRecordId}
    // =========================================================================

    /// <summary>Soft-deletes an inventory record.</summary>
    [HttpDelete("{inventoryRecordId:guid}")]
    [SwaggerOperation(
        Summary = "Delete inventory record",
        Description = "Soft-deletes the inventory record. " +
                      "Does NOT delete or deactivate the parent product. " +
                      "The record is hidden from all subsequent inventory queries.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteInventoryRecord(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid inventoryRecordId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var command = new DeleteInventoryRecordCommand(inventoryRecordId);
        var result = await Sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.FailureResponse(result.Message, result.Errors));

        return NoContent();
    }

    // =========================================================================
    // GET /api/retailers/{retailerId}/inventory/export/csv
    // =========================================================================

    /// <summary>Exports all inventory records as a CSV file download.</summary>
    [HttpGet("export/csv")]
    [Produces("text/csv")]
    [SwaggerOperation(
        Summary = "Export inventory as CSV",
        Description = "Streams all non-deleted inventory records for the retailer as a UTF-8 CSV file. " +
                      "Columns: InventoryRecordId, ProductId, ProductName, " +
                      "CurrentStock, SoldQuantity, LowStockThreshold, Status, CreatedAt. " +
                      "Uses server-side streaming — safe for large datasets.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var query = new ExportInventoryCsvQuery();

        // Pass HttpContext.RequestAborted — ensures the EF Core DataReader is disposed
        // immediately when the client disconnects mid-download.
        var csvBytes = await Sender.Send(query, HttpContext.RequestAborted);

        var fileName =
            $"inventory_{retailerId:N}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(csvBytes, "text/csv", fileName);
    }
}

// =============================================================================
// Request Contracts
// =============================================================================

/// <summary>
/// Request body for PATCH /inventory/{inventoryRecordId}/adjust.
/// </summary>
/// <param name="NewQuantity">
///   Absolute target stock level. Must be >= 0.
/// </param>
/// <param name="Type">
///   Category of adjustment: ManualIncrease | ManualDecrease | OrderSale | ReturnRestock.
/// </param>
/// <param name="Reason">
///   Human-readable reason. Required for ManualIncrease and ManualDecrease.
/// </param>
public sealed record AdjustStockRequest(
    int NewQuantity,
    string Type,
    string? Reason);