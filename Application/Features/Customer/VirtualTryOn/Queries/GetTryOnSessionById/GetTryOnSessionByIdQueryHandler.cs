using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Features.Customer.VirtualTryOn.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionById;

/// <summary>
/// Returns a single virtual try-on session by ID for the authenticated customer.
/// Cache-aside: TTL 10 minutes. Sessions are immutable once created.
/// </summary>
public sealed class GetTryOnSessionByIdQueryHandler
    : IRequestHandler<GetTryOnSessionByIdQuery, VirtualTryOnSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetTryOnSessionByIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<VirtualTryOnSessionDto> Handle(
        GetTryOnSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId
            ?? throw new UnauthorizedException("Customer identity missing.");

        string cacheKey = $"tryon_session:{request.SessionId:N}";

        var cached = await _cacheService.GetAsync<VirtualTryOnSessionDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            // IDOR guard even on cache hit — ensure the session belongs to the caller.
            if (cached.CustomerId != customerId)
                throw new NotFoundException("VirtualTryOnSession", request.SessionId);
            return cached;
        }

        var session = await _context.VirtualTryOnSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null || session.CustomerId != customerId)
            throw new NotFoundException("VirtualTryOnSession", request.SessionId);

        var dto = session.ToDto();

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10), cancellationToken);

        return dto;
    }
}
