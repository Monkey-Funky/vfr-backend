using Application.Features.Customer.Catalog.DTOs;

namespace Application.Features.Customer.Catalog.Queries.GetProductDetail;

public sealed record GetProductDetailQuery(Guid ProductId) : IRequest<ProductDetailDto>;
