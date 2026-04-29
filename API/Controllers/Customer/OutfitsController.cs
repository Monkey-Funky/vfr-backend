using API.Controllers.BaseControllers;
using Application.Features.Customer.Outfits.Commands.CreateOutfit;
using Application.Features.Customer.Outfits.Commands.DeleteOutfit;
using Application.Features.Customer.Outfits.Commands.UpdateOutfit;
using Application.Features.Customer.Outfits.DTOs;
using Application.Features.Customer.Outfits.Queries.GetOutfitDetail;
using Application.Features.Customer.Outfits.Queries.GetOutfits;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[SwaggerTag("Customer Outfits — manage wardrobe outfits.")]
[Route("api/customer/outfits")]
public sealed class OutfitsController : CustomerBaseApiController
{
    // =========================================================================
    // GET api/customer/outfits
    // =========================================================================

    [HttpGet]
    [SwaggerOperation(
        "Get Customer Outfits",
        "Gets a summary of all outfits belonging to the authenticated customer.")]
    [ProducesResponseType(typeof(ApiResponse<List<OutfitSummaryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOutfits(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetOutfitsQuery(), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // GET api/customer/outfits/{outfitId}
    // =========================================================================

    [HttpGet("{outfitId}")]
    [SwaggerOperation(
        "Get Outfit Detail",
        "Gets full details of a specific outfit, including cross-referenced product data.")]
    [ProducesResponseType(typeof(ApiResponse<OutfitDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOutfitDetail(Guid outfitId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetOutfitDetailQuery(outfitId), cancellationToken);
        return OkResponse(result);
    }

    // =========================================================================
    // POST api/customer/outfits
    // =========================================================================

    [HttpPost]
    [SwaggerOperation(
        "Create Outfit",
        "Creates a new outfit from favorited products.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateOutfit(
        [FromBody] CreateOutfitCommand command, 
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        // Using generic OkResponse for consistency with CustomerProfileController
        return OkResponse(result, "Outfit created successfully.");
    }

    // =========================================================================
    // PUT api/customer/outfits/{outfitId}
    // =========================================================================

    [HttpPut("{outfitId}")]
    [SwaggerOperation(
        "Update Outfit",
        "Updates an existing outfit by completely replacing its items.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateOutfit(
        Guid outfitId, 
        [FromBody] UpdateOutfitRequest request, 
        CancellationToken cancellationToken)
    {
        var command = new UpdateOutfitCommand(outfitId, request.Name, request.StyleCategory, request.Items);
        await Sender.Send(command, cancellationToken);
        return OkResponse(true, "Outfit updated successfully.");
    }

    // =========================================================================
    // DELETE api/customer/outfits/{outfitId}
    // =========================================================================

    [HttpDelete("{outfitId}")]
    [SwaggerOperation(
        "Delete Outfit",
        "Soft-deletes a specific outfit.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteOutfit(Guid outfitId, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteOutfitCommand(outfitId), cancellationToken);
        return OkResponse(true, "Outfit deleted successfully.");
    }
}

// Request wrapper for PUT so we don't need OutfitId in the body
public sealed record UpdateOutfitRequest(
    string? Name,
    string? StyleCategory,
    List<UpdateOutfitItemDto> Items
);
