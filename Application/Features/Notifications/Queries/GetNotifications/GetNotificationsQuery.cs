using Application.Features.Notifications.DTOs;

namespace Application.Features.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(
    bool? IsRead,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedResult<NotificationDto>>;