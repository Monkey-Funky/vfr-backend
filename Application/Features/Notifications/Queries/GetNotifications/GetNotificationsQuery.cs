using Application.Features.Notifications.DTOs;

namespace Application.Features.Notifications.Queries.GetNotifications;

/// <summary>
/// Returns a paginated list of notifications for the authenticated retailer.
/// UnreadCount is returned alongside the page via NotificationsPagedResult.
/// PageSize is capped at 50 per spec.
/// </summary>
public sealed record GetNotificationsQuery(
    bool? IsRead = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<NotificationsPagedResult>;

/// <summary>
/// Extended paged result that also carries the total unread count.
/// Returned by GetNotificationsQueryHandler so a single request
/// gives the caller both the list and the badge count.
/// </summary>
public sealed class NotificationsPagedResult
{
    public IReadOnlyList<NotificationDto> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}