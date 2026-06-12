using API.Controllers.BaseControllers;
using Application.Common;
using Application.Features.Customer.Avatar.Commands.CreateAvatar;
using Application.Features.Customer.Avatar.Commands.DeleteAvatar;
using Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;
using Application.Features.Customer.Avatar.Commands.UpdateAvatarMeasurements;
using Application.Features.Customer.Avatar.DTOs;
using Application.Features.Customer.Avatar.Queries.GetAvatar;
using Application.Features.Customer.Avatar.Queries.GetAvatarHistory;
using Application.Features.Customer.Avatar.Queries.GetSizeRecommendation;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Customer;

[Route("api/customers/{customerId:guid}/avatar")]
[SwaggerTag("Customer Avatar & Measurements — manage 3D body measurements and retrieve size estimations.")]
public sealed class AvatarController : CustomerBaseApiController
{
    // ==============================================================
    // GET api/customers/{customerId}/avatar
    // ==============================================================
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get active avatar",
        Description = "Returns the customer's current avatar and most recent body measurements. Returns 404 if no avatar exists.")]
    [ProducesResponseType(typeof(ApiResponse<AvatarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvatar(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        var result = await Sender.Send(new GetAvatarQuery(customerId), cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // GET api/customers/{customerId}/avatar/history
    // ==============================================================
    [HttpGet("history")]
    [SwaggerOperation(
        Summary = "Get measurement history",
        Description = "Returns an immutable chronological snapshot (paginated) of body measurement changes over time.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AvatarMeasurementHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvatarHistory(
        Guid customerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureCustomerOwnership(customerId);
        var result = await Sender.Send(
            new GetAvatarMeasurementHistoryQuery(customerId, pageNumber, pageSize), cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // POST api/customers/{customerId}/avatar
    // ==============================================================
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a new avatar",
        Description = "Initializes an avatar with the provided measurements and source. Fails if the customer already has an active avatar.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAvatar(
        Guid customerId,
        [FromBody] CreateAvatarCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        var resultId = await Sender.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAvatar), new { customerId }, new ApiResponse<Guid> { Success = true, Data = resultId });
    }

    // ==============================================================
    // PATCH api/customers/{customerId}/avatar/measurements
    // ==============================================================
    [HttpPatch("measurements")]
    [SwaggerOperation(
        Summary = "Update measurements",
        Description = "Overwrite the active avatar's measurements entirely, automatically capturing a new snapshot event in the history log.")]
    [ProducesResponseType(typeof(ApiResponse<EmptyResult>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateMeasurements(
        Guid customerId,
        [FromBody] UpdateAvatarMeasurementsCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        await Sender.Send(command, cancellationToken);
        return NoContentResponse();
    }

    // ==============================================================
    // DELETE api/customers/{customerId}/avatar
    // ==============================================================
    [HttpDelete]
    [SwaggerOperation(
        Summary = "Delete active avatar",
        Description = "Soft deletes the current avatar. Measurement histories remain untouched for audit trailing.")]
    [ProducesResponseType(typeof(ApiResponse<EmptyResult>), StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAvatar(
        Guid customerId,
        [FromBody] DeleteAvatarCommand command,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        await Sender.Send(command, cancellationToken);
        return NoContentResponse();
    }

    // ==============================================================
    // GET api/customers/{customerId}/avatar/size-recommendation/{productId}
    // ==============================================================
    [HttpGet("size-recommendation/{productId:guid}")]
    [SwaggerOperation(
        Summary = "Get a size recommendation",
        Description = "Uses ML mapping limits (placeholder) to match customer's body measurements to the product sizing chart.")]
    [ProducesResponseType(typeof(ApiResponse<SizeRecommendationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSizeRecommendation(
        Guid customerId,
        Guid productId,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);
        var result = await Sender.Send(new GetSizeRecommendationQuery(productId), cancellationToken);
        return OkResponse(result);
    }

    // ==============================================================
    // POST api/customers/{customerId}/avatar/extract-from-image
    // ==============================================================
    [HttpPost("extract-from-image")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Extract measurements from image",
        Description = "Uploads a full-body photo to an AI model that extracts body measurements. " +
                      "Creates a new avatar if none exists, or updates the existing one. " +
                      "The image is NOT persisted — it is streamed to the AI model and discarded.")]
    [ProducesResponseType(typeof(ApiResponse<AvatarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExtractMeasurementsFromImage(
        Guid customerId,
        [FromForm] ExtractMeasurementsFromImageRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        // Map IFormFile → FileUploadDto in the API layer.
        var frontImageUpload = new FileUploadDto(
            Content: request.FrontImage.OpenReadStream(),
            FileName: request.FrontImage.FileName,
            ContentType: request.FrontImage.ContentType,
            Length: request.FrontImage.Length);

        var sideImageUpload = new FileUploadDto(
            Content: request.SideImage.OpenReadStream(),
            FileName: request.SideImage.FileName,
            ContentType: request.SideImage.ContentType,
            Length: request.SideImage.Length);
        var command = new ExtractMeasurementsFromImageCommand(frontImageUpload, sideImageUpload, request.HeightCm);
        var result = await Sender.Send(command, cancellationToken);

        return OkResponse(result, "Measurements extracted and saved successfully.");
    }
}

/// <summary>
/// Request model for POST /api/customers/{customerId}/avatar/extract-from-image.
/// Bound from multipart/form-data.
/// </summary>
public sealed class ExtractMeasurementsFromImageRequest
{
    /// <summary>
    /// Front Image photo of the customer. JPEG or PNG. Max 5 MB.
    /// </summary>
    public IFormFile FrontImage { get; init; } = null!;
    /// <summary>
    /// Side Image photo of the customer. JPEG or PNG. Max 5 MB.
    /// </summary>
    public IFormFile SideImage { get; init; } = null!;

    /// <summary>
    /// The customer's actual height in centimeters (required for the AI model to scale estimates).
    /// </summary>
    public decimal HeightCm { get; init; }
}