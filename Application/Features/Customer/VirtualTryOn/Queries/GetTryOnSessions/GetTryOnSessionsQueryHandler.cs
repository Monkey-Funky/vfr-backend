using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Features.Customer.VirtualTryOn.Mappings;
using Application.Interfaces.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessions;

public sealed class GetTryOnSessionsQueryHandler : IRequestHandler<GetTryOnSessionsQuery, PagedResult<VirtualTryOnSessionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTryOnSessionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<VirtualTryOnSessionDto>> Handle(GetTryOnSessionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.VirtualTryOnSessions
            .AsNoTracking()
            .Where(s => s.CustomerId == request.CustomerId)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var sessions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = sessions.Select(s => s.ToDto()).ToList();

        return new PagedResult<VirtualTryOnSessionDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}
