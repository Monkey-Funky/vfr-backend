
namespace Application.Features.Notifications.Commands.DeleteNotification;

public sealed record DeleteNotificationCommand(Guid NotificationId)
    : IRequest<Result>;