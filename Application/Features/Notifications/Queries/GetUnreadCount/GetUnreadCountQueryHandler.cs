using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;


namespace Application.Features.Notifications.Queries.GetUnreadCount;


/// <summary>
/// Issues a DB-level COUNT(*) — NEVER loads Notification records into memory.
/// Verified via SQL logging: the generated query is
///   SELECT COUNT(*) FROM notifications WHERE retailer_id = @id AND is_read = false
/// </summary>
public sealed class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetUnreadCountQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<int> Handle(
        GetUnreadCountQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(
                n => n.RetailerId == retailerId && !n.IsRead,
                cancellationToken);
    }
}