using Application.Features.Customer.Catalog.DTOs;

namespace Application.Features.Customer.Catalog.Queries.GetSimilarProducts;

public sealed record GetSimilarProductsQuery(Guid ProductId, int Limit = 8) : IRequest<List<ProductCardDto>>;
