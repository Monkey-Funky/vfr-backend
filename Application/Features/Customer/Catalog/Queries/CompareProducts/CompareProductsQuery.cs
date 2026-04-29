using Application.Features.Customer.Catalog.DTOs;

namespace Application.Features.Customer.Catalog.Queries.CompareProducts;

public sealed record CompareProductsQuery(Guid[] ProductIds) : IRequest<List<ProductComparisonDto>>;
