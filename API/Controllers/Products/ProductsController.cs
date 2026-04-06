using API.Controllers.Products.Requests;
using Application.Common;
using Application.Features.Products.Commands.AddProductImage;
using Application.Features.Products.Commands.CreateProduct;
using Application.Features.Products.Commands.DeleteProduct;
using Application.Features.Products.Commands.RemoveProductImage;
using Application.Features.Products.Commands.ToggleProductStatus;
using Application.Features.Products.Commands.UpdateProduct;
using Application.Features.Products.DTOs;
using Application.Features.Products.Queries.GetProductById;
using Application.Features.Products.Queries.GetProducts;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Products;

/// <summary>
/// CRUD and image management for the authenticated retailer's product catalogue.
///
/// ALL ROUTES: /api/retailers/{retailerId:guid}/products
///
/// ENDPOINTS (10):
///   GET    /products                           — paginated list with FTS and filters
///   POST   /products                           — create product + inventory record (rate-limited: upload)
///   GET    /products/{productId}               — full detail with inventory summary
///   PUT    /products/{productId}               — full field update
///   PATCH  /products/{productId}               — partial field update (same command as PUT)
///   DELETE /products/{productId}               — soft-delete product + inventory record
///   PATCH  /products/{productId}/status        — toggle Active ↔ Inactive (Draft → Active)
///   GET    /products/{productId}/images        — list non-deleted product images
///   POST   /products/{productId}/images        — upload single image to S3 (rate-limited: upload)
///   DELETE /products/{productId}/images/{imageId} — soft-delete image + S3 delete (best-effort)
///
/// SECURITY:
///   All actions call EnsureRetailerOwnership(retailerId) as their first statement.
///   RetailerId is NEVER used from URL for business logic — only for IDOR verification.
///   Handlers always read RetailerId from ICurrentUserService (JWT sub claim).
///
/// RATE LIMITING:
///   POST /products and POST /products/{productId}/images carry
///   [EnableRateLimiting("upload")] — 20 requests/min per RetailerId.
///   All other actions inherit [EnableRateLimiting("api-global")] from BaseApiController.
/// </summary>
[SwaggerTag("Products — full catalogue management including image uploads")]
[Route("api/retailers/{retailerId:guid}/products")]
public sealed class ProductsController : BaseApiController
{
    // =========================================================================
    // 1. GET /products
    // =========================================================================

    /// <summary>
    /// Returns a paginated, filtered list of products for the authenticated retailer.
    /// Supports full-text search via plainto_tsquery on name, description, and barcode.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List products",
        Description = "Returns a paginated list of products for the authenticated retailer. " +
                      "Supports filtering by category, sub-category, and status. " +
                      "Full-text search (searchTerm) uses PostgreSQL plainto_tsquery on " +
                      "name + description + barcode. All results exclude soft-deleted records.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductListDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetProducts(
        Guid retailerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? subCategoryId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var result = await Sender.Send(
            new GetProductsQuery(
                PageNumber: pageNumber,
                PageSize: pageSize,
                CategoryId: categoryId,
                SubCategoryId: subCategoryId,
                Status: status,
                SearchTerm: searchTerm),
            cancellationToken);

        return OkResponse(result, "Products retrieved successfully.");
    }

    // =========================================================================
    // 2. POST /products  (rate-limited: upload)
    // =========================================================================

    /// <summary>
    /// Creates a new product with an optional initial batch of images.
    /// Enforces the active product cap from the retailer's subscription plan.
    /// Images are uploaded to S3 before the database transaction begins.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    [SwaggerOperation(
    Summary = "Create product",
    Description = "Creates a new product and its InventoryRecord in a single atomic transaction. " +
                  "Optional images are uploaded to S3 first; if the DB transaction fails, " +
                  "the S3 objects are orphaned and cleaned up by a scheduled job. " +
                  "Plan limit: throws 422 PRODUCT_LIMIT_EXCEEDED if the active product cap is reached. " +
                  "Upload rate limit: 20 requests/minute per retailer.")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CreateProduct(
    Guid retailerId,
    [FromForm] CreateProductRequest request,
    CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        // FIX: Map IFormFile[] → FileUploadDto[] here, in the API layer.
        //      OpenReadStream() is called on each IFormFile; the resulting streams
        //      are disposed by the handler via "await using var stream = file.Content".
        var imageUploads = request.Images?
            .Select(f => new FileUploadDto(
                Content: f.OpenReadStream(),
                FileName: f.FileName,
                ContentType: f.ContentType,
                Length: f.Length))
            .ToArray();

        var result = await Sender.Send(
            new CreateProductCommand(
                Name: request.Name,
                Description: request.Description,
                CategoryId: request.CategoryId,
                SubCategoryId: request.SubCategoryId,
                Price: request.Price,
                Currency: request.Currency,
                Barcode: request.Barcode,
                InitialQuantity: request.InitialQuantity,
                Status: request.Status,
                Images: imageUploads),   
            cancellationToken);

        return CreatedResponse(
            routeName: "GetProductById",
            routeValues: new { retailerId, productId = result.Data!.Id },
            data: result.Data);
    }

    // =========================================================================
    // 3. GET /products/{productId}
    // =========================================================================

    /// <summary>
    /// Returns the full detail DTO for a single product, including all
    /// non-deleted images and the current inventory summary.
    /// </summary>
    [HttpGet("{productId:guid}", Name = "GetProductById")]
    [SwaggerOperation(
        Summary = "Get product by ID",
        Description = "Returns the full product detail including images (ordered by DisplayOrder) " +
                      "and inventory summary (CurrentStock, InventoryStatus). " +
                      "Returns 404 if the product does not exist or belongs to another retailer.")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductById(
        Guid retailerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var result = await Sender.Send(
            new GetProductByIdQuery(productId),
            cancellationToken);

        return OkResponse(result, "Product retrieved successfully.");
    }

    // =========================================================================
    // 4. PUT /products/{productId}
    // =========================================================================

    /// <summary>
    /// Full field update for an existing product. All ShouldUpdate* flags must
    /// be set to true to replace all mutable fields.
    /// </summary>
    [HttpPut("{productId:guid}")]
    [SwaggerOperation(
        Summary = "Update product (full replace)",
        Description = "Updates one or more mutable fields of a product. " +
                      "Set ShouldUpdate{Field} = true for each field you want to overwrite. " +
                      "PUT semantics: caller should set all ShouldUpdate flags to true. " +
                      "Returns the updated product detail DTO.")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProduct(
        Guid retailerId,
        Guid productId,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var result = await Sender.Send(
            new UpdateProductCommand(
                ProductId: productId,
                NewName: request.NewName,
                NewDescription: request.NewDescription,
                ShouldUpdateDescription: request.ShouldUpdateDescription,
                NewPrice: request.NewPrice,
                ShouldUpdatePrice: request.ShouldUpdatePrice,
                NewBarcode: request.NewBarcode,
                ShouldUpdateBarcode: request.ShouldUpdateBarcode,
                NewCategoryId: request.NewCategoryId,
                ShouldUpdateCategory: request.ShouldUpdateCategory,
                NewSubCategoryId: request.NewSubCategoryId,
                NewStatus: request.NewStatus),
            cancellationToken);

        return OkResponse(result.Data!, "Product updated successfully.");
    }

    // =========================================================================
    // 5. PATCH /products/{productId}
    // =========================================================================

    /// <summary>
    /// Partial field update for an existing product. Only send the fields you
    /// want to change by setting their corresponding ShouldUpdate* flag to true.
    /// </summary>
    [HttpPatch("{productId:guid}")]
    [SwaggerOperation(
        Summary = "Update product (partial)",
        Description = "Partial update of a product's mutable fields. " +
                      "PATCH semantics: only send ShouldUpdate{Field} = true for fields you want to change. " +
                      "Fields whose ShouldUpdate flag is false are left untouched. " +
                      "Uses the same UpdateProductCommand as PUT — the distinction is purely semantic. " +
                      "Returns the updated product detail DTO.")]
    [ProducesResponseType(typeof(ApiResponse<ProductDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PatchProduct(
        Guid retailerId,
        Guid productId,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var result = await Sender.Send(
            new UpdateProductCommand(
                ProductId: productId,
                NewName: request.NewName,
                NewDescription: request.NewDescription,
                ShouldUpdateDescription: request.ShouldUpdateDescription,
                NewPrice: request.NewPrice,
                ShouldUpdatePrice: request.ShouldUpdatePrice,
                NewBarcode: request.NewBarcode,
                ShouldUpdateBarcode: request.ShouldUpdateBarcode,
                NewCategoryId: request.NewCategoryId,
                ShouldUpdateCategory: request.ShouldUpdateCategory,
                NewSubCategoryId: request.NewSubCategoryId,
                NewStatus: request.NewStatus),
            cancellationToken);

        return OkResponse(result.Data!, "Product updated successfully.");
    }

    // =========================================================================
    // 6. DELETE /products/{productId}
    // =========================================================================

    /// <summary>
    /// Soft-deletes a product and its InventoryRecord in a single atomic transaction.
    /// The product and its images remain in the database but are excluded from all queries.
    /// </summary>
    [HttpDelete("{productId:guid}")]
    [SwaggerOperation(
        Summary = "Delete product",
        Description = "Soft-deletes the product and its associated InventoryRecord atomically. " +
                      "The operation is irreversible through the public API. " +
                      "Images are NOT deleted from S3 — a scheduled cleanup job handles orphaned objects. " +
                      "Returns 204 No Content on success.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(
        Guid retailerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await Sender.Send(
            new DeleteProductCommand(productId),
            cancellationToken);

        return NoContentResponse();
    }

    // =========================================================================
    // 7. PATCH /products/{productId}/status
    // =========================================================================

    /// <summary>
    /// Toggles the product's lifecycle status. Active → Inactive, Inactive → Active.
    /// Draft products transition to Active on the first toggle.
    /// </summary>
    [HttpPatch("{productId:guid}/status")]
    [SwaggerOperation(
        Summary = "Toggle product status",
        Description = "Cycles the product's status: Active → Inactive → Active. " +
                      "Draft products transition directly to Active on the first call. " +
                      "Returns the new status string in the response data field.")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleProductStatus(
        Guid retailerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        var result = await Sender.Send(
            new ToggleProductStatusCommand(productId),
            cancellationToken);

        return OkResponse(result.Data!, result.Message);
    }

    // =========================================================================
    // 8. GET /products/{productId}/images
    // =========================================================================

    /// <summary>
    /// Returns the list of non-deleted images for a product, ordered by DisplayOrder.
    /// Re-uses GetProductByIdQuery and extracts the Images collection from the detail DTO.
    /// </summary>
    [HttpGet("{productId:guid}/images")]
    [SwaggerOperation(
        Summary = "List product images",
        Description = "Returns all non-deleted images for the specified product, " +
                      "ordered ascending by DisplayOrder. " +
                      "Returns 404 if the product does not exist or belongs to another retailer. " +
                      "This endpoint re-uses GetProductByIdQuery — for full product detail use " +
                      "GET /products/{productId} instead.")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProductImageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductImages(
        Guid retailerId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        // GetProductByIdQuery already loads images with the product (AsSplitQuery + Include).
        // Extracting Images avoids a dedicated query class for this slim read path.
        var detail = await Sender.Send(
            new GetProductByIdQuery(productId),
            cancellationToken);

        return OkResponse(detail.Images, "Product images retrieved successfully.");
    }

    // =========================================================================
    // 9. POST /products/{productId}/images  (rate-limited: upload)
    // =========================================================================

    /// <summary>
    /// Uploads a single image to S3 and adds it to the product's image list.
    /// Magic byte validation runs before any S3 upload.
    /// </summary>
    [HttpPost("{productId:guid}/images")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    [SwaggerOperation(
        Summary = "Add product image",
        Description = "Uploads a single JPEG or PNG image to S3 and attaches it to the product. " +
                      "FileStorageService reads the first 4 bytes (magic bytes) to verify " +
                      "the file is genuinely JPEG (FF D8 FF) or PNG (89 50 4E 47) — " +
                      "bypassing ContentType header spoofing. " +
                      "A GUID-based filename is generated to prevent path traversal attacks. " +
                      "The image is stored under the products/{retailerId}/ folder prefix in S3. " +
                      "Upload rate limit: 20 requests/minute per retailer.")]
    [ProducesResponseType(typeof(ApiResponse<ProductImageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> AddProductImage(
        Guid retailerId,
        Guid productId,
        [FromForm] AddProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        // FIX: Map IFormFile → FileUploadDto here, in the API layer.
        //      The stream is opened once and disposed by the handler.
        var imageUpload = new FileUploadDto(
            Content: request.ImageFile.OpenReadStream(),
            FileName: request.ImageFile.FileName,
            ContentType: request.ImageFile.ContentType,
            Length: request.ImageFile.Length);

        var result = await Sender.Send(
            new AddProductImageCommand(
                ProductId: productId,
                ImageFile: imageUpload,       // ← was: request.ImageFile (IFormFile)
                DisplayOrder: request.DisplayOrder),
            cancellationToken);

        return CreatedResponse(
            routeName: "GetProductById",
            routeValues: new { retailerId, productId },
            data: result.Data!);
    }

    // =========================================================================
    // 10. DELETE /products/{productId}/images/{imageId}
    // =========================================================================

    /// <summary>
    /// Soft-deletes the image record in the database and attempts to delete
    /// the underlying object from S3. S3 deletion is best-effort — if it fails,
    /// the image is already soft-deleted in the DB and will not appear in responses.
    /// </summary>
    [HttpDelete("{productId:guid}/images/{imageId:guid}")]
    [SwaggerOperation(
        Summary = "Remove product image",
        Description = "Soft-deletes the product image record and performs a best-effort S3 delete. " +
                      "DB soft-delete happens first; S3 failure is caught, logged as Warning, " +
                      "and does not roll back the DB change. " +
                      "Orphaned S3 objects are reconciled by a scheduled cleanup job. " +
                      "Returns 204 No Content on success.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProductImage(
        Guid retailerId,
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        await Sender.Send(
            new RemoveProductImageCommand(
                ProductId: productId,
                ImageId: imageId),
            cancellationToken);

        return NoContentResponse();
    }
}