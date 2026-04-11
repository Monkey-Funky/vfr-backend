using Application.Features.Notifications.Commands.DeleteNotification;
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
    // =========================================================================
    // GET /api/notifications
    // =========================================================================

    /// <summary>Returns a paginated list of notifications with total unread count.</summary>
    [HttpGet]
    [SwaggerOperation(Summary = "Get notifications",
        Description = "Returns paginated notifications. UnreadCount is always the total " +
                      "unread count regardless of the IsRead filter. PageSize max 50.")]
    [ProducesResponseType(typeof(ApiResponse<NotificationsPagedResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetNotificationsQuery(isRead, pageNumber, pageSize);
        var result = await Sender.Send(query, cancellationToken);
        return Ok(ApiResponse<NotificationsPagedResult>.SuccessResponse(result));
    }

    // =========================================================================
    // PUT /api/notifications/{id}/read
    // =========================================================================

    /// <summary>Marks a single notification as read.</summary>
    [HttpPut("{id:guid}/read")]
    [SwaggerOperation(Summary = "Mark notification as read")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new MarkNotificationReadCommand(id);
        var result = await Sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<bool>.FailureResponse(result.Message, result.Errors));

        return Ok(ApiResponse<bool>.SuccessResponse(true, result.Message));
    }

    // =========================================================================
    // PUT /api/notifications/read-all
    // =========================================================================

    /// <summary>Marks all unread notifications as read using a bulk SQL UPDATE.</summary>
    [HttpPut("read-all")]
    [SwaggerOperation(Summary = "Mark all notifications as read",
        Description = "Uses ExecuteUpdateAsync — no in-memory loop. Returns count of rows updated.")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var command = new MarkAllNotificationsReadCommand();
        var result = await Sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse<int>.FailureResponse(result.Message, result.Errors));

        return Ok(ApiResponse<int>.SuccessResponse(result.Data, result.Message));
    }

    // =========================================================================
    // DELETE /api/notifications/{id}
    // =========================================================================

    /// <summary>Hard-deletes a single notification.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Summary = "Delete notification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteNotification(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteNotificationCommand(id);
        var result = await Sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(ApiResponse<object>.FailureResponse(result.Message));

        return NoContent();
    }
}