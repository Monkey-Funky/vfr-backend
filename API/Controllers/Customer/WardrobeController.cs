using API.Controllers.BaseControllers;
using Application.Features.Customer.Wardrobe.Commands.AddItemToCollection;
using Application.Features.Customer.Wardrobe.Commands.CreateCollection;
using Application.Features.Customer.Wardrobe.Commands.DeleteCollection;
using Application.Features.Customer.Wardrobe.Commands.RemoveItemFromCollection;
using Application.Features.Customer.Wardrobe.Commands.RenameCollection;
using Application.Features.Customer.Wardrobe.DTOs;
using Application.Features.Customer.Wardrobe.Queries.GetCollectionItems;
using Application.Features.Customer.Wardrobe.Queries.GetCollections;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[Route("api/customers/{customerId:guid}/wardrobe/collections")]
[SwaggerTag("Wardrobe Collections — Manage custom collections of favorited items.")]
public sealed class WardrobeController : CustomerBaseApiController
{
    // ==============================================================
    // GET api/customers/{customerId}/wardrobe/collections
    // ==============================================================
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get Collections",
        Description = "Retrieves all wardrobe collections for the authenticated customer."
    )]
    [ProducesResponseType(typeof(ApiResponse<List<WardrobeCollectionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCollections(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var query = new GetCollectionsQuery();
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // POST api/customers/{customerId}/wardrobe/collections
    // ==============================================================
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create Collection",
        Description = "Creates a new wardrobe collection for the authenticated customer."
    )]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCollection(
        Guid customerId,
        [FromBody] CreateCollectionCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.SuccessResponse(result, "Collection created successfully."));
    }

    // ==============================================================
    // PATCH api/customers/{customerId}/wardrobe/collections/{collectionId}
    // ==============================================================
    [HttpPatch("{collectionId:guid}")]
    [SwaggerOperation(
        Summary = "Rename Collection",
        Description = "Renames an existing wardrobe collection."
    )]
    [ProducesResponseType(typeof(ApiResponse<EmptyResult>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RenameCollection(
        Guid customerId,
        Guid collectionId,
        [FromBody] RenameCollectionRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var command = new RenameCollectionCommand(collectionId, request.NewName);
        await Sender.Send(command, cancellationToken);
        return NoContentResponse();
    }

    // ==============================================================
    // DELETE api/customers/{customerId}/wardrobe/collections/{collectionId}
    // ==============================================================
    [HttpDelete("{collectionId:guid}")]
    [SwaggerOperation(
        Summary = "Delete Collection",
        Description = "Soft-deletes a wardrobe collection and removes its items."
    )]
    [ProducesResponseType(typeof(ApiResponse<EmptyResult>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeleteCollection(
        Guid customerId,
        Guid collectionId,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var command = new DeleteCollectionCommand(collectionId);
        await Sender.Send(command, cancellationToken);
        return NoContentResponse();
    }

    // ==============================================================
    // GET api/customers/{customerId}/wardrobe/collections/{collectionId}/items
    // ==============================================================
    [HttpGet("{collectionId:guid}/items")]
    [SwaggerOperation(
        Summary = "Get Collection Items",
        Description = "Retrieves a paginated list of items inside a specific collection. Each item's \"id\" is the row UUID needed for DELETE /items/{id}."
    )]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CollectionItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetCollectionItems(
        Guid customerId,
        Guid collectionId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerOwnership(customerId);

        var query = new GetCollectionItemsQuery(collectionId, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // POST api/customers/{customerId}/wardrobe/collections/{collectionId}/items
    // ==============================================================
    [HttpPost("{collectionId:guid}/items")]
    [SwaggerOperation(
        Summary = "Add Item to Collection",
        Description = "Adds a product to a collection, automatically favoriting it if necessary."
    )]
    [ProducesResponseType(typeof(ApiResponse<EmptyResult>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddItemToCollection(
        Guid customerId,
        Guid collectionId,
        [FromBody] AddItemToCollectionRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var command = new AddItemToCollectionCommand(collectionId, request.ProductId);
        await Sender.Send(command, cancellationToken);
        return NoContentResponse();
    }

    // ==============================================================
    // DELETE api/customers/{customerId}/wardrobe/collections/{collectionId}/items/{itemId}
    // ==============================================================
    [HttpDelete("{collectionId:guid}/items/{itemId:guid}")]
    [SwaggerOperation(
        Summary = "Remove Item from Collection",
        Description = "Removes an item from a collection by the item row UUID (returned as \"id\" from GET /items). Does not unfavorite the product."
    )]
    [ProducesResponseType(typeof(ApiResponse<EmptyResult>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveItemFromCollection(
        Guid customerId,
        Guid collectionId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var command = new RemoveItemFromCollectionCommand(collectionId, itemId);
        await Sender.Send(command, cancellationToken);
        return NoContentResponse();
    }
}

public sealed record RenameCollectionRequestDto(string NewName);
public sealed record AddItemToCollectionRequestDto(Guid ProductId);
