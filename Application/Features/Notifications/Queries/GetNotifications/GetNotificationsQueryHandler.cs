using Application.Features.Notifications.DTOs;
using Application.Interfaces.Persistence;
using Application.Interfaces.Services;
using Domain.Entities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Notifications.Queries.GetNotifications;

public sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, PagedResult<NotificationDto>>
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

    public async Task<PagedResult<NotificationDto>> Handle(
        GetNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        Guid retailerId = _currentUserService.RetailerId
            ?? throw new UnauthorizedException("Retailer identity could not be resolved.");

        string cacheKey =
            $"notifications:{retailerId}:" +
            $"p{query.PageNumber}s{query.PageSize}" +
            $":read{query.IsRead?.ToString().ToLower() ?? "null"}";

        PagedResult<NotificationDto>? cached =
            await _cacheService.GetAsync<PagedResult<NotificationDto>>(cacheKey, cancellationToken);

        if (cached is not null)
            return cached;

        IQueryable<Notification> queryable = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RetailerId == retailerId);

        if (query.IsRead.HasValue)
            queryable = queryable.Where(n => n.IsRead == query.IsRead.Value);

        queryable = queryable.OrderByDescending(n => n.CreatedAt);

        int totalCount = await queryable.CountAsync(cancellationToken);

        List<NotificationDto> items = await queryable
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(n => n.ToDto())
            .ToListAsync(cancellationToken);

        // FIX: PagedResult<T> is a class with init properties — use object initializer,
        //      NOT a positional/named-parameter constructor call.
        PagedResult<NotificationDto> result = new()
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), cancellationToken);

        return result;
    }
}