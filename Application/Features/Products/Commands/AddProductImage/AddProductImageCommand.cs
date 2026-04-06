using Application.Common;
using Application.Features.Products.DTOs;

namespace Application.Features.Products.Commands.AddProductImage;


/// <summary>
/// Uploads a single image to S3 and adds the resulting URL to the product's image list.
/// IDOR guard: ProductId is scoped to the authenticated retailer.
/// </summary>
public sealed record AddProductImageCommand(
    Guid ProductId,
    FileUploadDto ImageFile,
    int DisplayOrder = 0
) : IRequest<Result<ProductImageDto>>;