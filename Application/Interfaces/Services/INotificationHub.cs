using Application.Features.Notifications.DTOs;

namespace Application.Interfaces.Services;

/// <summary>
/// Abstraction over the SignalR hub used to push real-time notifications.
/// Command handlers reference this interface — never the concrete hub directly.
/// </summary>
public interface INotificationHub
{
    /// <summary>
    /// Pushes a notification to all SignalR connections in the retailer's group.
    /// </summary>
    Task SendNotificationAsync(string retailerId, NotificationDto notification);
}