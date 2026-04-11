
namespace Application.Features.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandValidator
    : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(c => c.NotificationId)
            .NotEmpty()
            .WithMessage("NotificationId is required.");
    }
}