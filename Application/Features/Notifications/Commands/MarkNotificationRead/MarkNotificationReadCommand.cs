namespace Application.Features.Notifications.Commands.MarkNotificationRead;
public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Result<bool>>;
