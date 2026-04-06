using Application.Features.Categories.Commands.CreateCategory;
using Application.Features.Categories.Commands.CreateSubCategory;
using Application.Features.Categories.Commands.DeleteCategory;
using Application.Features.Categories.Commands.DeleteSubCategory;
using Application.Features.Categories.Commands.ToggleCategoryStatus;
using Application.Features.Categories.Commands.UpdateCategory;
using Application.Features.Categories.Commands.UpdateSubCategory;
using Application.Features.Categories.DTOs;
using Application.Features.Categories.Queries.GetCategories;
using Application.Features.Categories.Queries.GetCategoryById;
using Application.Features.Categories.Queries.GetSubCategories;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Categories;

/// <summary>
/// Manages product categories and sub-categories for the authenticated retailer.
/// All endpoints are retailer-scoped and protected by JWT RS256 (Retailer role).
/// </summary>
[SwaggerTag("Categories — manage product categories and sub-categories")]
[Route("api/retailers/{retailerId:guid}/categories")]
public sealed class CategoriesController : BaseApiController
{
    // =========================================================================
    // Category Endpoints
    // =========================================================================

    /// <summary>GET api/retailers/{retailerId}/categories</summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List categories",
        Description = "Returns a paginated list of categories for the retailer, " +
                      "optionally filtered by status. Results are cached for 30 minutes.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetCategories(
        [FromRoute] Guid retailerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        PagedResult<CategoryDto> result = await Sender.Send(
            new GetCategoriesQuery(pageNumber, pageSize, status),
            cancellationToken);

        return OkResponse(result);
    }

    /// <summary>GET api/retailers/{retailerId}/categories/{categoryId}</summary>
    [HttpGet("{categoryId:guid}", Name = "GetCategoryById")]
    [SwaggerOperation(
        Summary = "Get category by ID",
        Description = "Returns a single category with its sub-category count. " +
                      "Returns 404 if the category does not exist or belongs to another retailer.")]
    [ProducesResponseType(typeof(ApiResponse<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryById(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        CategoryDto dto = await Sender.Send(
            new GetCategoryByIdQuery(categoryId),
            cancellationToken);

        return OkResponse(dto);
    }

    /// <summary>POST api/retailers/{retailerId}/categories</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Create a category",
        Description = "Creates a new category. Uploads the cover image to S3 and " +
                      "stores the public URL. Returns 409 if the name already exists for this retailer.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCategory(
        [FromRoute] Guid retailerId,
        [FromForm] CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await using Stream imageStream = request.CoverImageFile.OpenReadStream();

        Result<Guid> result = await Sender.Send(
            new CreateCategoryCommand(
                Name: request.Name,
                Description: request.Description,
                CoverImageStream: imageStream,
                CoverImageFileName: request.CoverImageFile.FileName,
                CoverImageContentType: request.CoverImageFile.ContentType,
                Status: request.Status),
            cancellationToken);

        return CreatedResponse(
            "GetCategoryById",
            new { retailerId, categoryId = result.Data },
            result.Data);
    }

    /// <summary>PUT api/retailers/{retailerId}/categories/{categoryId}</summary>
    [HttpPut("{categoryId:guid}")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(
        Summary = "Update a category",
        Description = "Updates an existing category. All fields are optional — only " +
                      "supplied fields are changed. If a new cover image is provided, " +
                      "the old image is deleted from S3.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCategory(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        [FromForm] UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Stream? imageStream = null;

        try
        {
            if (request.NewCoverImageFile is not null)
                imageStream = request.NewCoverImageFile.OpenReadStream();

            Result<bool> result = await Sender.Send(
                new UpdateCategoryCommand(
                    CategoryId: categoryId,
                    NewName: request.NewName,
                    NewDescription: request.NewDescription,
                    ShouldUpdateDescription: request.ShouldUpdateDescription,
                    NewCoverImageStream: imageStream,
                    NewCoverImageFileName: request.NewCoverImageFile?.FileName,
                    NewCoverImageContentType: request.NewCoverImageFile?.ContentType,
                    Status: request.Status),
                cancellationToken);

            return OkResponse(result.Data);
        }
        finally
        {
            if (imageStream is not null)
                await imageStream.DisposeAsync();
        }
    }

    /// <summary>DELETE api/retailers/{retailerId}/categories/{categoryId}</summary>
    [HttpDelete("{categoryId:guid}")]
    [SwaggerOperation(
        Summary = "Delete a category",
        Description = "Soft-deletes a category and cascades: " +
                      "offers with this category → Inactive, " +
                      "products.CategoryId → null, " +
                      "all sub-categories → soft-deleted. All in one transaction.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await Sender.Send(new DeleteCategoryCommand(categoryId), cancellationToken);

        return NoContentResponse();
    }

    /// <summary>PATCH api/retailers/{retailerId}/categories/{categoryId}/toggle-status</summary>
    [HttpPatch("{categoryId:guid}/toggle-status")]
    [SwaggerOperation(
        Summary = "Toggle category status",
        Description = "Switches the category status between Active and Inactive. " +
                      "Returns the newly applied status value.")]
    [ProducesResponseType(typeof(ApiResponse<CategoryStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleCategoryStatus(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<CategoryStatusDto> result = await Sender.Send(
            new ToggleCategoryStatusCommand(categoryId),
            cancellationToken);

        return OkResponse(result.Data);
    }

    // =========================================================================
    // Sub-Category Endpoints
    // =========================================================================

    /// <summary>GET api/retailers/{retailerId}/categories/{categoryId}/sub-categories</summary>
    [HttpGet("{categoryId:guid}/sub-categories")]
    [SwaggerOperation(
        Summary = "List sub-categories",
        Description = "Returns all sub-categories for the given parent category. " +
                      "Returns 404 if the parent category does not exist or belongs to another retailer.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SubCategoryDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubCategories(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        IReadOnlyList<SubCategoryDto> result = await Sender.Send(
            new GetSubCategoriesQuery(categoryId),
            cancellationToken);

        return OkResponse(result);
    }

    /// <summary>POST api/retailers/{retailerId}/categories/{categoryId}/sub-categories</summary>
    [HttpPost("{categoryId:guid}/sub-categories")]
    [SwaggerOperation(
        Summary = "Create a sub-category",
        Description = "Creates a sub-category under the specified parent category. " +
                      "Enforces max depth = 1 and name uniqueness within the parent. " +
                      "Returns 404 if the parent category does not belong to this retailer.")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateSubCategory(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        [FromBody] CreateSubCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<Guid> result = await Sender.Send(
            new CreateSubCategoryCommand(
                ParentCategoryId: categoryId,
                Name: request.Name,
                Status: request.Status),
            cancellationToken);

        // FIX-2: Corrected argument order to match BaseApiController.CreatedResponse<T> signature:
        //   CreatedResponse<T>(string routeName, object routeValues, T data)
        // Previously the arguments were passed as (result.Data, routeName, routeValues)
        // which caused CS1503: Argument 1 cannot convert from 'Guid' to 'string'.
        return CreatedResponse(
            "GetCategoryById",
            new { retailerId, categoryId },
            result.Data);
    }

    /// <summary>PUT api/retailers/{retailerId}/categories/{categoryId}/sub-categories/{subCategoryId}</summary>
    [HttpPut("{categoryId:guid}/sub-categories/{subCategoryId:guid}")]
    [SwaggerOperation(
        Summary = "Update a sub-category",
        Description = "Updates the Name and/or Status of an existing sub-category. " +
                      "Returns 404 if the sub-category does not belong to this retailer " +
                      "or does not belong to the specified parent category.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSubCategory(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        [FromRoute] Guid subCategoryId,
        [FromBody] UpdateSubCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(
            new UpdateSubCategoryCommand(
                ParentCategoryId: categoryId,   // BUG-004 FIX: was not passed before
                SubCategoryId: subCategoryId,
                NewName: request.NewName,
                Status: request.Status),
            cancellationToken);

        return OkResponse(result.Data);
    }

    /// <summary>DELETE api/retailers/{retailerId}/categories/{categoryId}/sub-categories/{subCategoryId}</summary>
    [HttpDelete("{categoryId:guid}/sub-categories/{subCategoryId:guid}")]
    [SwaggerOperation(
        Summary = "Delete a sub-category",
        Description = "Soft-deletes a sub-category. " +
                      "Cascade: products.SubCategoryId → null in the same transaction.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubCategory(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid categoryId,
        [FromRoute] Guid subCategoryId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await Sender.Send(
            new DeleteSubCategoryCommand(
                ParentCategoryId: categoryId,   
                SubCategoryId: subCategoryId),
            cancellationToken);

        return NoContentResponse();
    }
}