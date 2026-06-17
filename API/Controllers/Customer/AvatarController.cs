using API.Controllers.BaseControllers;
using Application.Common;
using Application.Features.Customer.Avatar.Commands.CreateAvatar;
using Application.Features.Customer.Avatar.Commands.DeleteAvatar;
using Application.Features.Customer.Avatar.Commands.ExtractMeasurementsFromImage;
using Application.Features.Customer.Avatar.Commands.RepairAvatarSourceImage;
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
    [HttpGet(Name = "GetAvatar")]
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
        return CreatedResponse("GetAvatar", new { customerId }, resultId);
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
        Summary = "Extract measurements from images",
        Description =
            "Uploads TWO full-body photos (front view + side view) to an AI model that extracts body measurements. " +
            "Creates a new avatar if none exists, or updates the existing one. " +
            "The front image is persisted to storage as the avatar's source image " +
            "(required for both 2D Overlay try-on and 3D SAM Align). " +
            "Uses a two-phase save: measurements + SourceImageUrl are written to the database BEFORE " +
            "the optional 3D model generation step, so a fal.ai timeout never silently destroys try-on capability.")]
    [ProducesResponseType(typeof(ApiResponse<AvatarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExtractMeasurementsFromImage(
        Guid customerId,
        [FromForm] ExtractMeasurementsFromImageRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var frontImageUpload = new FileUploadDto(
            Content: request.FrontImageFile.OpenReadStream(),
            FileName: request.FrontImageFile.FileName,
            ContentType: request.FrontImageFile.ContentType,
            Length: request.FrontImageFile.Length);

        var sideImageUpload = new FileUploadDto(
            Content: request.SideImageFile.OpenReadStream(),
            FileName: request.SideImageFile.FileName,
            ContentType: request.SideImageFile.ContentType,
            Length: request.SideImageFile.Length);

        var command = new ExtractMeasurementsFromImageCommand(frontImageUpload, sideImageUpload, request.HeightCm);
        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result, "Measurements extracted and saved successfully.");
    }

    // ==============================================================
    // POST api/customers/{customerId}/avatar/repair-source-image
    // ==============================================================
    [HttpPost("repair-source-image")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Repair avatar source image",
        Description =
            "Uploads a new front-facing photo for an existing avatar whose SourceImageUrl is null. " +
            "This restores 2D try-on (Overlay2D) capability without re-extracting measurements. " +
            "Use this to fix avatars created before the two-phase-save fix was deployed, " +
            "or any avatar where fal.ai timed out during the original extract-from-image call. " +
            "Check Has2DCapability on GET /avatar before calling — returns 422 if a source image already exists. " +
            "Set RetryGenerate3D=true (default) to also attempt regeneration of the 3D body model.")]
    [ProducesResponseType(typeof(ApiResponse<AvatarDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RepairAvatarSourceImage(
        Guid customerId,
        [FromForm] RepairAvatarSourceImageRequest request,
        CancellationToken cancellationToken)
    {
        EnsureCustomerOwnership(customerId);

        var frontImageUpload = new FileUploadDto(
            Content: request.FrontImageFile.OpenReadStream(),
            FileName: request.FrontImageFile.FileName,
            ContentType: request.FrontImageFile.ContentType,
            Length: request.FrontImageFile.Length);

        var command = new RepairAvatarSourceImageCommand(
            FrontImageFile: frontImageUpload,
            RetryGenerate3D: request.RetryGenerate3D);

        var result = await Sender.Send(command, cancellationToken);
        return OkResponse(result, "Avatar source image repaired successfully.");
    }
}

/// <summary>
/// Request model for POST /api/customers/{customerId}/avatar/extract-from-image.
/// Bound from multipart/form-data.
/// </summary>
public sealed class ExtractMeasurementsFromImageRequest
{
    /// <summary>
    /// Front-facing full-body photo. JPEG or PNG. Max 10 MB.
    /// The customer should face directly toward the camera with arms slightly away from the body.
    /// </summary>
    public IFormFile FrontImageFile { get; init; } = null!;

    /// <summary>
    /// Side-view full-body photo. JPEG or PNG. Max 10 MB.
    /// The customer should stand 90° to the side with arms slightly away from the body.
    /// </summary>
    public IFormFile SideImageFile { get; init; } = null!;

    /// <summary>
    /// The customer's actual height in centimeters (required for the AI model to scale estimates).
    /// </summary>
    public decimal HeightCm { get; init; }
}

/// <summary>
/// Request model for POST /api/customers/{customerId}/avatar/repair-source-image.
/// Bound from multipart/form-data.
/// </summary>
public sealed class RepairAvatarSourceImageRequest
{
    /// <summary>
    /// Front-facing full-body photo. JPEG or PNG. Max 10 MB.
    /// </summary>
    public IFormFile FrontImageFile { get; init; } = null!;

    /// <summary>
    /// When true (default) the handler will attempt to re-generate the 3D body
    /// model via fal.ai after the upload succeeds.
    /// </summary>
    public bool RetryGenerate3D { get; init; } = true;
}
