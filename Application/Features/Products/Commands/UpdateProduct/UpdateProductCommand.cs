
using Application.Features.Products.DTOs;

namespace Application.Features.Products.Commands.UpdateProduct;


/// <summary>
/// Updates mutable fields on an existing product.
/// All fields are optional — only those with a non-null value are applied.
/// The exception: Description and Price use explicit "should update" flags
/// to distinguish "do not change" from "explicitly clear".
///
/// IDOR guard: ProductId is resolved against the retailer's JWT identity.
/// </summary>
public sealed record UpdateProductCommand(
    Guid ProductId,
    string? NewName,
    string? NewDescription,
    bool ShouldUpdateDescription,
    decimal? NewPrice,
    bool ShouldUpdatePrice,
    string? NewBarcode,
    bool ShouldUpdateBarcode,
    Guid? NewCategoryId,
    bool ShouldUpdateCategory,
    Guid? NewSubCategoryId,
    string? NewStatus
) : IRequest<Result<ProductDetailDto>>;