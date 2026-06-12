using API.Controllers.BaseControllers;
using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Outfits.Commands.CreateOutfit;
using Application.Features.Customer.Outfits.Commands.DeleteOutfit;
using Application.Features.Customer.Outfits.Commands.UpdateOutfit;
using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.Outfits.Queries.GetOutfitDetail;
using Application.Features.Customer.Outfits.Queries.GetOutfits;
using Application.Features.Customer.OutfitSuggestions.Queries.GetComplementaryOutfits;
using MediatR;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

/// <summary>
/// Manages wardrobe outfits for an individual customer.
///
/// Route design: the {customerId} segment in the URL allows the API to be
/// clearly scoped to a customer resource. <see cref="EnsureCustomerOwnership"/>
/// is called on every action so that the value in the URL is validated against
/// the authenticated JWT claim, providing IDOR protection — a request from
/// customer A carrying customer B's ID in the path is rejected with 403.
/// </summary>
[SwaggerTag("Customer Outfits — manage wardrobe outfits.")]
[Route("api/customers/{customerId}/outfits")]
public sealed class OutfitsController : CustomerBaseApiController
{
    // =========================================================================
    // GET api/customers/{customerId}/outfits
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        "Get Customer Outfits",
        "Gets a paged summary of all outfits belonging to the authenticated customer.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OutfitSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOutfits(Guid customerId, CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var result = await Sender.Send(new GetOutfitsQuery(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // GET api/customers/{customerId}/outfits/{outfitId}
    // =========================================================================

    [HttpGet("{outfitId:guid}")]
    [SwaggerOperation(
        "Get Outfit Detail",
        "Gets full details of a specific outfit, including cross-referenced product data.")]
    [ProducesResponseType(typeof(ApiResponse<OutfitDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOutfitById(Guid customerId, Guid outfitId, CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var result = await Sender.Send(new GetOutfitDetailQuery(outfitId), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customers/{customerId}/outfits
    // =========================================================================

    [HttpPost]
    [SwaggerOperation(
        "Create Outfit",
        "Creates a new outfit from favorited products. Returns 201 Created with the new outfit's ID.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateOutfit(
        Guid customerId,
        [FromBody] CreateOutfitCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiResponse<Guid>.SuccessResponse(result, "Outfit created successfully."));
    }

    // =========================================================================
    // PUT api/customers/{customerId}/outfits/{outfitId}
    // =========================================================================

    [HttpPut("{outfitId:guid}")]
    [SwaggerOperation(
        "Update Outfit",
        "Updates an existing outfit by completely replacing its items.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateOutfit(
        Guid customerId,
        Guid outfitId,
        [FromBody] UpdateOutfitRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var command = new UpdateOutfitCommand(outfitId, request.Name, request.StyleCategory, request.Items);
        await Sender.Send(command, cancellationToken);
        return OkResponse(true, "Outfit updated successfully.");
    }

    // =========================================================================
    // DELETE api/customers/{customerId}/outfits/{outfitId}
    // =========================================================================

    [HttpDelete("{outfitId:guid}")]
    [SwaggerOperation(
        "Delete Outfit",
        "Soft-deletes a specific outfit. Returns 204 No Content on success.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteOutfit(Guid customerId, Guid outfitId, CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        await Sender.Send(new DeleteOutfitCommand(outfitId), cancellationToken);
        return NoContentResponse();
    }
    // =========================================================================
    // Get api/customers/{customerId}/outfits/complementary
    // =========================================================================
    [HttpGet("complementary")]
    [SwaggerOperation(
        Summary = "Get AI complementary style recommendations",
        Description = "Returns a list of products that visually match the target product.")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductCardDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplementaryOutfits(
        [FromQuery] GetComplementaryOutfitsQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(query, cancellationToken);

        return Ok(result);
    }
}

// ---------------------------------------------------------------------------
// Request DTO for PUT — keeps OutfitId out of the body (it lives in the route)
// ---------------------------------------------------------------------------

/// <summary>
/// Request body for the Update Outfit endpoint.
/// <c>OutfitId</c> is intentionally absent — it is bound from the route segment.
/// </summary>
public sealed record UpdateOutfitRequest(
    string? Name,
    string? StyleCategory,
    List<UpdateOutfitItemDto> Items
);