using Application.Features.Customer.VirtualTryOn.DTOs;
using Application.Features.Customer.VirtualTryOn.Mappings;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Customer.VirtualTryOn.Queries.GetTryOnSessionById;

public sealed class GetTryOnSessionByIdQueryHandler : IRequestHandler<GetTryOnSessionByIdQuery, VirtualTryOnSessionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTryOnSessionByIdQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<VirtualTryOnSessionDto> Handle(GetTryOnSessionByIdQuery request, CancellationToken cancellationToken)
    {
        var customerId = _currentUserService.CustomerId 
            ?? throw new UnauthorizedException("Customer identity missing.");

        var session = await _context.VirtualTryOnSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

        if (session is null || session.CustomerId != customerId)
            throw new NotFoundException("VirtualTryOnSession", request.SessionId);

        return session.ToDto();
    }
}
