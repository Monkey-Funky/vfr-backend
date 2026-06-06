using API.Controllers.BaseControllers;
using Application.Features.Customer.Catalog.DTOs;
using Application.Features.Customer.Catalog.Queries.BrowseCategories;
using Application.Features.Customer.Catalog.Queries.BrowseOffers;
using Application.Features.Customer.Catalog.Queries.BrowseProducts;
using Application.Features.Customer.Catalog.Queries.CompareProducts;
using Application.Features.Customer.Catalog.Queries.GetProductDetail;
using Application.Features.Customer.Catalog.Queries.GetProductsByModelIds;
using Application.Features.Customer.Catalog.Queries.GetSimilarProducts;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[Route("api/catalog")]
[AllowAnonymous]
[SwaggerTag("Catalog Browsing — Public e-commerce listing, product details, search, and comparisons.")]
public class CatalogController : CoreBaseApiController
{
    // ==============================================================
    // GET api/catalog/products
    // ==============================================================
    [HttpGet("products")]
    [SwaggerOperation(
        Summary = "Browse products",
        Description = "Returns a paginated list of products based on comprehensive multi-select filters, price ranges, and full-text search.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductCardDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BrowseProducts([FromQuery] BrowseProductsQuery query, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/catalog/products/{productId}
    // ==============================================================
    [HttpGet("products/{productId:guid}")]
    [SwaggerOperation(
        Summary = "Get product details",
        Description = "Retrieves full details for a single product, including attributes, images, stock status, and active offers. Automatically increments the view count.")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductDetail(Guid productId, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetProductDetailQuery(productId), cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/catalog/products/{productId}/similar
    // ==============================================================
    [HttpGet("products/{productId:guid}/similar")]
    [SwaggerOperation(
        Summary = "Get similar products",
        Description = "Recommends similar products based on matching category or brand, sorted by popularity (views).")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductCardDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimilarProducts(Guid productId, [FromQuery] int limit = 8, CancellationToken cancellationToken = default)
    {
        var result = await Sender.Send(new GetSimilarProductsQuery(productId, limit), cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // POST api/catalog/products/compare
    // ==============================================================
    [HttpPost("products/compare")]
    [SwaggerOperation(
        Summary = "Compare products",
        Description = "Accepts an array of Product IDs (between 2 and 4) and returns their specifications side-by-side.")]
    [ProducesResponseType(typeof(ApiResponse<List<ProductComparisonDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompareProducts([FromBody] CompareProductsQuery query, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // POST api/catalog/products/by-model-ids
    // ==============================================================
    [HttpPost("products/by-model-ids")]
    [SwaggerOperation(
        Summary = "Resolve AI style-recommendation model IDs to products",
        Description = """
            Accepts the JSON payload returned by the AI style-recommendation model and
            resolves each model ID (e.g. "78_y3ppkj") to a full product card.

            **Expected request body:**
            ```json
            { "modelIds": ["78_y3ppkj", "66_d3xdez", "39_kuchvf"] }
            ```

            **Behaviour:**
            - Results are returned in the same order as the input list (rank-preserving).
            - Unknown or inactive model IDs are silently skipped.
            - Maximum 50 model IDs per request.
            - The endpoint is public (no authentication required); favorite flags are
              populated only when a customer Bearer token is present.
            """)]
    [ProducesResponseType(typeof(ApiResponse<List<ProductCardDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetProductsByModelIds(
        [FromBody] ResolveModelIdsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new GetProductsByModelIdsQuery(request.ResolvedIds),
            cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/catalog/categories
    // ==============================================================
    [HttpGet("categories")]
    [SwaggerOperation(
        Summary = "Browse categories",
        Description = "Returns an aggregated list of categories along with their respective product counts.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CategoryBrowseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BrowseCategories([FromQuery] BrowseCategoriesQuery query, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/catalog/offers
    // ==============================================================
    [HttpGet("offers")]
    [SwaggerOperation(
        Summary = "Browse active offers",
        Description = "Returns a paginated list of currently active promotional offers.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OfferBrowseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BrowseOffers([FromQuery] BrowseOffersQuery query, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(query, cancellationToken);
        return OkResponse(result);
    }
}

// ==================================================================
// Request DTO — local to this controller file to keep things minimal
// ==================================================================

/// <summary>
/// Request body for POST api/catalog/products/by-model-ids.
/// Designed to accept the exact shape returned by the AI model:
/// <code>{ "matches": ["78_y3ppkj", "66_d3xdez"] }</code>
/// as well as a more explicit form:
/// <code>{ "modelIds": ["78_y3ppkj", "66_d3xdez"] }</code>
/// Both fields are accepted (whichever the caller populates).
/// </summary>
public sealed class ResolveModelIdsRequest
{
    /// <summary>
    /// Explicit field name — used by our own front-end calls.
    /// Example: <c>{ "modelIds": ["78_y3ppkj", "66_d3xdez"] }</c>
    /// </summary>
    public IReadOnlyList<string>? ModelIds { get; init; }

    /// <summary>
    /// Raw field name emitted by the AI model response.
    /// Example: <c>{ "matches": ["78_y3ppkj", "66_d3xdez"] }</c>
    /// </summary>
    public IReadOnlyList<string>? Matches { get; init; }

    /// <summary>
    /// Resolved list — ModelIds takes precedence over Matches.
    /// Falls back to an empty list if both are null.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<string> ResolvedIds =>
        ModelIds is { Count: > 0 }
            ? ModelIds
            : Matches ?? Array.Empty<string>();
}