
using Application.Common;
using Application.Features.Products.DTOs;

namespace Application.Features.Products.Commands.CreateProduct;

/// <summary>
/// Creates a new product for the authenticated retailer.
///
/// Plan limit check:  Enforced inside ExecuteInTransactionAsync (TOCTOU-safe).
/// Atomicity:         Product + InventoryRecord created in ONE transaction.
/// Images:            S3 upload happens BEFORE the transaction. Orphaned objects
///                    are cleaned by a scheduled job if the transaction rolls back.
/// IDOR:              RetailerId is NEVER a command field — always from JWT.
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