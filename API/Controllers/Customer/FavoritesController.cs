using API.Controllers.BaseControllers;
using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Favorites.Commands.ToggleFavorite;
using Application.Features.Customer.Favorites.Queries.CheckFavorites;
using Application.Features.Customer.Favorites.Queries.GetFavorites;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[Route("api/customers/{customerId:guid}/favorites")]
[SwaggerTag("Customer Favorites — Add, remove, and view favorite products.")]
public sealed class FavoritesController : CustomerBaseApiController
{
    // ==============================================================
    // POST api/customers/{customerId}/favorites/toggle
    // ==============================================================
    [HttpPost("toggle")]
    [SwaggerOperation(
        Summary = "Toggle Favorite",
        Description = "Adds a product to favorites if it doesn't exist, or removes it if it does."
    )]
    [ProducesResponseType(typeof(ApiResponse<ToggleFavoriteResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ToggleFavorite(
        Guid customerId,
        [FromBody] ToggleFavoriteRequestDto request, 
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var command = new ToggleFavoriteCommand(request.ProductId);
        var result = await Sender.Send(command, cancellationToken);

        return OkResponse(new ToggleFavoriteResponseDto(result.IsFavoriteNow),
            result.IsFavoriteNow ? "Product added to favorites." : "Product removed from favorites.");
    }

    // ==============================================================
    // GET api/customers/{customerId}/favorites
    // ==============================================================
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get Favorites",
        Description = "Retrieves a paginated list of the customer's favorite products."
    )]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductCardDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetFavorites(
        Guid customerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerOwnership(customerId);

        var query = new GetFavoritesQuery(pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // POST api/customers/{customerId}/favorites/check
    // ==============================================================
    [HttpPost("check")]
    [SwaggerOperation(
        Summary = "Check Favorites",
        Description = "Bulk checks which of the provided product IDs are favorited by the customer."
    )]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<Guid, bool>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CheckFavorites(
        Guid customerId,
        [FromBody] CheckFavoritesRequestDto request, // <-- Changed to DTO
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var query = new CheckFavoritesQuery(request.ProductIds);
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }
}

// ==============================================================
// DTO Contracts (Clean boundary for the HTTP Layer)
// ==============================================================
public sealed record ToggleFavoriteRequestDto(Guid ProductId);
public sealed record CheckFavoritesRequestDto(Guid[] ProductIds);
public sealed record ToggleFavoriteResponseDto(bool IsFavorite);