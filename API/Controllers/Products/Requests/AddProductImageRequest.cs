namespace API.Controllers.Products.Requests;

/// <summary>
/// Request model for POST /api/retailers/{retailerId}/products/{productId}/images.
/// Bound from multipart/form-data.
///
/// CLIENT CONTRACT:
///   Content-Type: multipart/form-data
///   ImageFile: the image file (required). JPEG or PNG. Max 1 MB.
///   DisplayOrder: integer sort position (optional, defaults to 0).
/// </summary>
public sealed class AddProductImageRequest
{
    /// <summary>
    /// The image file to upload.
    /// FileStorageService validates magic bytes before uploading to S3.
    /// </summary>
    public IFormFile ImageFile { get; init; } = null!;

    /// <summary>
    /// Sort order among the product's images. Lower value = displayed first.
    /// Defaults to 0. Must be >= 0.
    /// </summary>
    public int DisplayOrder { get; init; }
}