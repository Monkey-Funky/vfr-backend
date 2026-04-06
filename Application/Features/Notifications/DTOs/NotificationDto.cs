using Domain.Entities.Notifications;


namespace Application.Features.Notifications.DTOs;

/// <summary>
/// Read model returned by <c>GetNotificationsQuery</c>.
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    bool IsRead,
    DateTime? ReadAt,
    Guid? ResourceId,
    DateTime CreatedAt);

public static class NotificationMappingExtensions
{
    public static NotificationDto ToDto(this Notification notification)
    {
        return new NotificationDto(
            Id: notification.Id,
            Type: notification.Type,
            Title: notification.Title,
            Body: notification.Body,
            IsRead: notification.IsRead,
            ReadAt: notification.ReadAt,
            ResourceId: notification.ResourceId,
            CreatedAt: notification.CreatedAt);
    }
}