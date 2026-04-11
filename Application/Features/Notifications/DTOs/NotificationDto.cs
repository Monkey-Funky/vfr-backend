using Domain.Entities.Notifications;


namespace Application.Features.Notifications.DTOs;

/// <summary>Read model returned by GetNotificationsQuery.</summary>
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
    public static NotificationDto ToDto(this Notification n) => new(
        Id: n.Id,
        Type: n.Type,
        Title: n.Title,
        Body: n.Body,
        IsRead: n.IsRead,
        ReadAt: n.ReadAt,
        ResourceId: n.ResourceId,
        CreatedAt: n.CreatedAt);
}