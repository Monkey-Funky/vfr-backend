using Application.Features.Customer.VirtualTryOn.DTOs;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionsByProduct;

public sealed record GetTryOnSessionsByProductQuery(
    Guid ProductId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<VirtualTryOnSessionDto>>;
