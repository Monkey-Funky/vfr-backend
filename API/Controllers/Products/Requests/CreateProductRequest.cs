namespace API.Controllers.Products.Requests;

/// <summary>
/// Request model for POST /api/retailers/{retailerId}/products.
/// Bound from multipart/form-data because the request can include image files.
///
/// CLIENT CONTRACT:
///   Content-Type: multipart/form-data
///   All text fields are included as form fields.
///   Images (optional) are included as file fields with the key "images".
///   Supports up to 5 images per create request (enforced by CreateProductCommandValidator).
/// </summary>
public sealed class CreateProductRequest
{
    /// <summary>Product display name. Required. Max 200 chars.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Free-text description. Optional. Max 2000 chars.</summary>
    public string? Description { get; init; }

    /// <summary>Optional FK to a Category owned by this retailer.</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>Optional FK to a SubCategory. Requires CategoryId to be set.</summary>
    public Guid? SubCategoryId { get; init; }

    /// <summary>Unit price. Optional — product may be created without a price.</summary>
    public decimal? Price { get; init; }

    /// <summary>ISO 4217 currency code. Defaults to EGP.</summary>
    public string Currency { get; init; } = "EGP";

    /// <summary>Barcode string. Optional. Max 100 chars. Must be alphanumeric.</summary>
    public string? Barcode { get; init; }

    /// <summary>Starting stock quantity. Defaults to 0. Must be >= 0.</summary>
    public int InitialQuantity { get; init; }

    /// <summary>
    /// Lifecycle status. Must be one of: Active, Inactive, Draft.
    /// Defaults to Draft.
    /// </summary>
    public string Status { get; init; } = "Draft";

    /// <summary>
    /// Optional product images. Max 1 MB each. JPEG / PNG only.
    /// Magic byte validation occurs in FileStorageService before any S3 upload.
    /// </summary>
    public IFormFile[]? Images { get; init; }
}