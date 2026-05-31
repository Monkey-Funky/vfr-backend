using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Features.Customer.VirtualTryOn.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionsByProduct;

/// <summary>
/// Returns paginated try-on sessions for a specific product and the authenticated customer.
/// Cache-aside: TTL 5 minutes. Invalidated by InitiateTryOn command.
/// </summary>
public sealed class GetTryOnSessionsByProductQueryHandler
    : IRequestHandler<GetTryOnSessionsByProductQuery, PagedResult<VirtualTryOnSessionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetTryOnSessionsByProductQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<PagedResult<VirtualTryOnSessionDto>> Handle(
        GetTryOnSessionsByProductQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        string cacheKey =
            $"tryon_product:{request.ProductId:N}:{customerId:N}:p{request.PageNumber}s{request.PageSize}";

        var cached = await _cacheService.GetAsync<PagedResult<VirtualTryOnSessionDto>>(
            cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var query = _context.VirtualTryOnSessions
            .AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.ProductId == request.ProductId)
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
