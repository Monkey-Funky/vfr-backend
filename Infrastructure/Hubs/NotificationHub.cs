using Application.Features.Notifications.DTOs;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time notification delivery.
/// Clients join the group "retailer_{retailerId}" on connection.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        string? retailerId = Context.User?.FindFirst("sub")?.Value
                          ?? Context.User?.FindFirst(
                                 System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(retailerId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"retailer_{retailerId}");

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? retailerId = Context.User?.FindFirst("sub")?.Value
                          ?? Context.User?.FindFirst(
                                 System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(retailerId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"retailer_{retailerId}");

        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Implements <see cref="INotificationHub"/> by delegating to the SignalR
/// <see cref="IHubContext{NotificationHub}"/>. Push calls are fire-and-forget:
/// callers must NOT await this service's methods inside a DB transaction.
/// </summary>
public sealed class NotificationHubService : INotificationHub
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationHubService> _logger;

    public NotificationHubService(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendNotificationAsync(string retailerId, NotificationDto notification)
    {
        try
        {
            await _hubContext.Clients
                .Group($"retailer_{retailerId}")
                .SendAsync("ReceiveNotification", notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SignalR push failed for Retailer {RetailerId}. " +
                "Notification was persisted to DB; real-time delivery only skipped.",
                retailerId);
        }
    }
}