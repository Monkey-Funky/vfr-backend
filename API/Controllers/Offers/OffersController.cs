using API.Controllers.BaseControllers;
using API.Controllers.Offers.Requests;
using Application.Common;
using Application.Features.Offers.Commands.CreateOffer;
using Application.Features.Offers.Commands.DeleteOffer;
using Application.Features.Offers.Commands.ToggleOfferStatus;
using Application.Features.Offers.Commands.UpdateOffer;
using Application.Features.Offers.DTOs;
using Application.Features.Offers.Queries.GetOfferById;
using Application.Features.Offers.Queries.GetOffers;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Offers;

/// <summary>
/// Offers management endpoints for the authenticated retailer.
/// All routes are scoped to the retailer identified by the JWT 'sub' claim.
/// IDOR protection: EnsureRetailerOwnership is called as the first statement
/// in every action — route-supplied retailerId must match the JWT claim.
/// </summary>
[Route("api/retailers/{retailerId:guid}/offers")]
[SwaggerTag("Offers — create, retrieve, update, toggle, and delete promotional offers.")]
public sealed class OffersController : RetailerBaseApiController
{
    // ── GET /api/retailers/{retailerId}/offers ─────────────────────────────────

    /// <summary>
    /// Returns a paginated list of offers for the authenticated retailer.
    /// Optional filters: Status, OfferType.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get offers",
        Description = "Returns a paginated list of offers scoped to the authenticated retailer. " +
                      "Supports optional filtering by Status ('Active', 'Inactive', 'Expired') " +
                      "and OfferType ('Product', 'Category').")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OfferDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetOffers(
        Guid retailerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? offerType = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        PagedResult<OfferDto> result = await Sender.Send(
            new GetOffersQuery(pageNumber, pageSize, status, offerType),
            cancellationToken);

        return OkResponse(result);
    }

    // ── GET /api/retailers/{retailerId}/offers/{offerId} ───────────────────────

    /// <summary>
    /// Returns the full details of a single offer by its ID.
    /// </summary>
    [HttpGet("{offerId:guid}", Name = "GetOfferById")]
    [SwaggerOperation(
        Summary = "Get offer by ID",
        Description = "Returns the full details of the specified offer, scoped to the " +
                      "authenticated retailer. Returns 404 if the offer does not exist " +
                      "or does not belong to this retailer.")]
    [ProducesResponseType(typeof(ApiResponse<OfferDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOfferById(
        Guid retailerId,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        OfferDto dto = await Sender.Send(
            new GetOfferByIdQuery(offerId),
            cancellationToken);

        return OkResponse(dto);
    }

    // ── POST /api/retailers/{retailerId}/offers ────────────────────────────────

    /// <summary>
    /// Creates a new promotional offer for the authenticated retailer.
    /// Accepts multipart/form-data (image + fields).
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Create offer",
        Description = "Creates a new promotional offer. " +
                      "For Product-type offers, the product must be Active and owned by this retailer. " +
                      "Fixed-type discount cannot exceed the product's price. " +
                      "Start date must not be in the past.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateOffer(
        Guid retailerId,
        [FromForm] CreateOfferRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        // ── Map IFormFile → FileUploadDto (API layer responsibility) ─────────
        // IFormFile is an ASP.NET Core type that must not cross into Application.
        // OpenReadStream() hands ownership of the stream to the FileUploadDto;
        // IFileStorageService.UploadAsync disposes it after use.
        var coverImage = new FileUploadDto(
            Content: request.CoverImage.OpenReadStream(),
            FileName: request.CoverImage.FileName,
            ContentType: request.CoverImage.ContentType,
            Length: request.CoverImage.Length);

        Result<Guid> result = await Sender.Send(
            new CreateOfferCommand(
                Title: request.Title,
                Description: request.Description,
                OfferType: request.OfferType,
                ProductId: request.ProductId,
                CategoryId: request.CategoryId,
                DiscountType: request.DiscountType,
                DiscountValue: request.DiscountValue,
                StartDate: request.StartDate,
                EndDate: request.EndDate,
                CoverImage: coverImage),    
            cancellationToken);

        return CreatedResponse(
            "GetOfferById",
            new { retailerId, offerId = result.Data },
            result.Data);
    }

    // ── PUT /api/retailers/{retailerId}/offers/{offerId} ───────────────────────

    /// <summary>
    /// Updates an existing offer's metadata, discount settings, dates, status, or image.
    /// OfferType and target (ProductId/CategoryId) are immutable after creation.
    /// Expired offers cannot be updated.
    /// </summary>
    [HttpPut("{offerId:guid}")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Update offer",
        Description = "Updates offer metadata (title, description), discount settings, " +
                      "date range, status, and optionally replaces the cover image. " +
                      "OfferType and target entity are immutable. Expired offers return 422.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateOffer(
        Guid retailerId,
        Guid offerId,
        [FromForm] UpdateOfferRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        // ── Map IFormFile? → FileUploadDto? (null = keep existing image) ─────
        FileUploadDto? coverImage = null;

        if (request.CoverImage is not null)
        {
            coverImage = new FileUploadDto(
                Content: request.CoverImage.OpenReadStream(),
                FileName: request.CoverImage.FileName,
                ContentType: request.CoverImage.ContentType,
                Length: request.CoverImage.Length);
        }

        await Sender.Send(
            new UpdateOfferCommand(
                OfferId: offerId,
                Title: request.Title,
                Description: request.Description,
                DiscountType: request.DiscountType,
                DiscountValue: request.DiscountValue,
                StartDate: request.StartDate,
                EndDate: request.EndDate,
                Status: request.Status,
                CoverImage: coverImage),      
            cancellationToken);

        return NoContentResponse();
    }

    // ── DELETE /api/retailers/{retailerId}/offers/{offerId} ────────────────────

    /// <summary>
    /// Soft-deletes the specified offer.
    /// The offer is excluded from all future reads via the global query filter.
    /// </summary>
    [HttpDelete("{offerId:guid}")]
    [SwaggerOperation(
        Summary = "Delete offer",
        Description = "Soft-deletes the specified offer. The row is retained in the database " +
                      "with is_deleted = true and is excluded from all future reads.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOffer(
        Guid retailerId,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await Sender.Send(
            new DeleteOfferCommand(offerId),
            cancellationToken);

        return NoContentResponse();
    }

    // ── PATCH /api/retailers/{retailerId}/offers/{offerId}/toggle-status ───────

    /// <summary>
    /// Toggles the offer status between Active and Inactive.
    /// Expired offers cannot be toggled — returns 422.
    /// </summary>
    [HttpPatch("{offerId:guid}/toggle-status")]
    [SwaggerOperation(
        Summary = "Toggle offer status",
        Description = "Toggles the offer status between Active and Inactive. " +
                      "An offer in Expired status cannot be toggled — it must remain Expired " +
                      "until the retailer creates a new replacement offer.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ToggleOfferStatus(
        Guid retailerId,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await Sender.Send(
            new ToggleOfferStatusCommand(offerId),
            cancellationToken);

        return NoContentResponse();
    }
}