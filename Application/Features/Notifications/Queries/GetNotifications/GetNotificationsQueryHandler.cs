using Application.Features.Notifications.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, NotificationsPagedResult>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICacheService _cacheService;

    public GetNotificationsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICacheService cacheService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cacheService = cacheService;
    }

    public async Task<NotificationsPagedResult> Handle(
        GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"notifications:{retailerId}:" +
            $"p{query.PageNumber}s{query.PageSize}" +
            $":read{query.IsRead?.ToString().ToLower() ?? "null"}";

        var cached = await _cacheService
            .GetAsync<NotificationsPagedResult>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        // ── Base queryable filtered by retailer first ────────────────────────
        var baseQuery = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RetailerId == retailerId);

        // ── Unread count (separate lightweight query) ─────────────────────────
        int unreadCount = await baseQuery
            .CountAsync(n => !n.IsRead, cancellationToken);

        // ── Apply IsRead filter ───────────────────────────────────────────────
        if (query.IsRead.HasValue)
            baseQuery = baseQuery.Where(n => n.IsRead == query.IsRead.Value);

        // ── Newest first ──────────────────────────────────────────────────────
        baseQuery = baseQuery.OrderByDescending(n => n.CreatedAt);

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        List<NotificationDto> items = await baseQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => n.ToDto())
            .ToListAsync(cancellationToken);

        var result = new NotificationsPagedResult
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            UnreadCount = unreadCount
        };

        await _cacheService.SetAsync(
            cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);

        return result;
    }
}