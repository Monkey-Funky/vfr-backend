using Application.Features.Products.DTOs;

namespace Application.Features.Products.Queries.GetProductById;


/// <summary>
/// Returns a single product's full detail DTO, including images and inventory summary.
/// Enforces IDOR: only the owning retailer can retrieve their own product.
/// </summary>
/// <param name="ProductId">The ID of the product to fetch.</param>
public sealed record GetProductByIdQuery(Guid ProductId)
    : IRequest<ProductDetailDto>;