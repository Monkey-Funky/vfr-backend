using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Features.Customer.VirtualTryOn.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessions;

/// <summary>
/// Returns paginated virtual try-on sessions for a customer.
/// Cache-aside: TTL 5 minutes. Invalidated by CreateTryOnSession command.
/// </summary>
public sealed class GetTryOnSessionsQueryHandler : IRequestHandler<GetTryOnSessionsQuery, PagedResult<VirtualTryOnSessionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;

    public GetTryOnSessionsQueryHandler(IApplicationDbContext context, ICacheService cacheService)
    {
        _context = context;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<VirtualTryOnSessionDto>> Handle(GetTryOnSessionsQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"tryon:{request.CustomerId:N}:p{request.PageNumber}s{request.PageSize}";

        var cached = await _cacheService.GetAsync<PagedResult<VirtualTryOnSessionDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var query = _context.VirtualTryOnSessions
            .AsNoTracking()
            .Where(s => s.CustomerId == request.CustomerId)
            .OrderByDescending(s => s.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var sessions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<VirtualTryOnSessionDto>
        {
            Items = sessions.Select(s => s.ToDto()).ToList(),
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);

        return result;
    }
}
