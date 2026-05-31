using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;

namespace Application.Features.Notifications.Queries.GetUnreadCount;

/// <summary>
/// Returns the unread notification count for the authenticated retailer.
/// Cache-aside: TTL 30 seconds (short-lived — count changes frequently on new notifications).
/// Invalidated by MarkNotificationRead and MarkAllNotificationsRead command handlers.
/// </summary>
public sealed class GetUnreadCountQueryHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetUnreadCountQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<int> Handle(
        GetUnreadCountQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey = CacheKeys.UnreadNotificationCount(retailerId);

        // Use a boxed int wrapper since ICacheService requires class types
        var cached = await _cacheService.GetAsync<UnreadCountDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached.Count;

        int count = await _context.Notifications
            .AsNoTracking()
            .CountAsync(
                n => n.RetailerId == retailerId && !n.IsRead,
                cancellationToken);

        await _cacheService.SetAsync(
            cacheKey,
            new UnreadCountDto(count),
            TimeSpan.FromSeconds(30),
            cancellationToken);

        return count;
    }

    // Private DTO to satisfy the class constraint on ICacheService
    private sealed record UnreadCountDto(int Count);
}
