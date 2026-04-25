using Application.Features.Customer.VirtualTryOn.DTOs;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessions;

public sealed record GetTryOnSessionsQuery(
    Guid CustomerId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<VirtualTryOnSessionDto>>;
