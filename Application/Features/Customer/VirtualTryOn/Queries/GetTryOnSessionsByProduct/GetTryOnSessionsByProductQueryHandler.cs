using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Features.Customer.VirtualTryOn.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionsByProduct;

public sealed class GetTryOnSessionsByProductQueryHandler : IRequestHandler<GetTryOnSessionsByProductQuery, PagedResult<VirtualTryOnSessionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTryOnSessionsByProductQueryHandler(
        IApplicationDbContext context, 
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<VirtualTryOnSessionDto>> Handle(GetTryOnSessionsByProductQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        var query = _context.VirtualTryOnSessions
            .AsNoTracking()
            .Where(s => s.CustomerId == customerId && s.ProductId == request.ProductId)
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
