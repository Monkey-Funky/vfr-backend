
using Application.Common;
using Application.Features.Products.DTOs;

namespace Application.Features.Products.Commands.CreateProduct;


/// <summary>
/// Creates a new product for the authenticated retailer.
///
/// Plan limit check:  Enforced before creation — throws BusinessRuleException
///                    with code "PRODUCT_LIMIT_EXCEEDED" if the retailer has
///                    reached their active product cap.
///
/// Atomicity:         Product + InventoryRecord are created in ONE transaction.
///
/// Images:            All images are uploaded to S3 BEFORE the transaction begins.
///                    If the transaction fails, the uploaded S3 objects are orphaned
///                    (accepted trade-off; a scheduled cleanup job handles these).
///
/// IDOR:              RetailerId is NEVER accepted as a command field — always from JWT.
/// </summary>
public sealed record CreateProductCommand(
    string Name,
    string? Description,
    Guid? CategoryId,
    Guid? SubCategoryId,
    decimal? Price,
    string Currency,
    string? Barcode,
    int InitialQuantity,
    string Status,
    FileUploadDto[]? Images        
) : IRequest<Result<ProductDetailDto>>;
