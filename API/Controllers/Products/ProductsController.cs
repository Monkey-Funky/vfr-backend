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

    /// <summary>Returns a paginated, filtered product list with optional FTS.</summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "List products",
        Description = "Returns a paginated list of products for the authenticated retailer. " +
                      "Supports filtering by categoryId, subCategoryId, and status. " +
                      "Full-text search (searchTerm) uses PostgreSQL plainto_tsquery " +
                      "on the pre-computed search_vector column (GIN-indexed). " +
                      "Produces ≤ 2 SQL statements per request.")]
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

    /// <summary>Creates a new product with an optional initial batch of images.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    [SwaggerOperation(
        Summary = "Create product",
        Description = "Creates a new product and its InventoryRecord in a single atomic transaction. " +
                      "Plan limit enforced inside the transaction (TOCTOU-safe). " +
                      "Returns 201 with Location header pointing to GET /products/{productId}.")]
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

        // Map IFormFile[] → FileUploadDto[] in the API layer.
        // OpenReadStream() is called here; handler disposes each stream via "await using".
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

    /// <summary>Returns the full detail DTO for a single product.</summary>
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

    /// <summary>Full field update for an existing product.</summary>
    [HttpPut("{productId:guid}")]
    [SwaggerOperation(
        Summary = "Update product (full replace)",
        Description = "Updates one or more mutable fields of a product. " +
                      "Set ShouldUpdate{Field} = true for each field you want to overwrite. " +
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

    /// <summary>Partial field update for an existing product.</summary>
    [HttpPatch("{productId:guid}")]
    [SwaggerOperation(
        Summary = "Update product (partial)",
        Description = "Partial update. Only send ShouldUpdate{Field} = true for fields you want to change.")]
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

    /// <summary>Soft-deletes a product, its images, inventory record, and inactivates offers.</summary>
    [HttpDelete("{productId:guid}")]
    [SwaggerOperation(
        Summary = "Delete product",
        Description = "Soft-deletes the product, all child ProductImage records, the InventoryRecord, " +
                      "and sets any active Offers for this product to Inactive — atomically in one transaction. " +
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

    /// <summary>Toggles the product's lifecycle status.</summary>
    [HttpPatch("{productId:guid}/status")]
    [SwaggerOperation(
        Summary = "Toggle product status",
        Description = "Cycles the product status: Active → Inactive → Active. " +
                      "Draft and OutOfStock products transition to Active on the first call. " +
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

    /// <summary>Returns non-deleted images for a product, ordered by DisplayOrder.</summary>
    [HttpGet("{productId:guid}/images")]
    [SwaggerOperation(
        Summary = "List product images",
        Description = "Returns all non-deleted images for the specified product, " +
                      "ordered ascending by DisplayOrder.")]
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

        var detail = await Sender.Send(
            new GetProductByIdQuery(productId),
            cancellationToken);

        return OkResponse(detail.Images, "Product images retrieved successfully.");
    }

    // =========================================================================
    // 9. POST /products/{productId}/images  (rate-limited: upload)
    // =========================================================================

    /// <summary>Uploads a single image to S3 and attaches it to the product.</summary>
    [HttpPost("{productId:guid}/images")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("upload")]
    [SwaggerOperation(
        Summary = "Add product image",
        Description = "Uploads a single JPEG or PNG image (max 5 MB) to S3 and attaches it to the product. " +
                      "Magic byte validation runs before any S3 upload. " +
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

        // Map IFormFile → FileUploadDto in the API layer.
        var imageUpload = new FileUploadDto(
            Content: request.ImageFile.OpenReadStream(),
            FileName: request.ImageFile.FileName,
            ContentType: request.ImageFile.ContentType,
            Length: request.ImageFile.Length);

        var result = await Sender.Send(
            new AddProductImageCommand(
                ProductId: productId,
                ImageFile: imageUpload,
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

    /// <summary>Soft-deletes the image DB record and performs a best-effort S3 delete.</summary>
    [HttpDelete("{productId:guid}/images/{imageId:guid}")]
    [SwaggerOperation(
        Summary = "Remove product image",
        Description = "Soft-deletes the product image record and performs a best-effort S3 delete. " +
                      "DB soft-delete happens first; S3 failure is caught and does not roll back. " +
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