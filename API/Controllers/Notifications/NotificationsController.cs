using Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using Application.Features.Notifications.Commands.MarkNotificationRead;
using Application.Features.Notifications.DTOs;
using Application.Features.Notifications.Queries.GetNotifications;
using Application.Features.Notifications.Queries.GetUnreadCount;
using Swashbuckle.AspNetCore.Annotations;

namespace API.Controllers.Notifications;

[Route("api/retailers/{retailerId:guid}/notifications")]
[SwaggerTag("Notification inbox — real-time alerts for low stock, orders, subscriptions, and payments.")]
public sealed class NotificationsController : BaseApiController
{
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get notifications",
        Description = "Returns a paginated list of notifications for the authenticated retailer. " +
                      "Optionally filter by read status.")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetNotifications(
        [FromRoute] Guid retailerId,
        [FromQuery] bool? isRead,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        PagedResult<NotificationDto> result = await Sender.Send(
            new GetNotificationsQuery(isRead, pageNumber, pageSize),
            cancellationToken);

        return OkResponse(result);
    }

    [HttpGet("unread-count")]
    [SwaggerOperation(
        Summary = "Get unread notification count",
        Description = "Returns the count of unread notifications using a DB-level COUNT(*). " +
                      "Never loads notification records into memory.")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUnreadCount(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        int count = await Sender.Send(new GetUnreadCountQuery(), cancellationToken);

        return OkResponse(count);
    }

    [HttpPatch("{notificationId:guid}/read")]
    [SwaggerOperation(
        Summary = "Mark notification as read",
        Description = "Marks a single notification as read. Idempotent — calling on an " +
                      "already-read notification returns 200. IDOR-protected.")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarkNotificationRead(
        [FromRoute] Guid retailerId,
        [FromRoute] Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<bool> result = await Sender.Send(
            new MarkNotificationReadCommand(notificationId),
            cancellationToken);

        return OkResponse(result.Data);
    }

    [HttpPatch("read-all")]
    [SwaggerOperation(
        Summary = "Mark all notifications as read",
        Description = "Bulk-marks all unread notifications for the retailer as read. " +
                      "Returns the number of notifications updated.")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAllNotificationsRead(
        [FromRoute] Guid retailerId,
        CancellationToken cancellationToken = default)
    {
        EnsureRetailerOwnership(retailerId);

        Result<int> result = await Sender.Send(
            new MarkAllNotificationsReadCommand(),
            cancellationToken);

        return OkResponse(result.Data);
    }
}