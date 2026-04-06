

namespace Application.Features.Settings.Commands.UpdateNotificationPreferences;

/// <summary>
/// Ensures that at least one field is specified for the PATCH update.
/// Sending a fully empty body would be a no-op and is considered a client error.
/// </summary>
public sealed class UpdateNotificationPreferencesCommandValidator
    : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.LowStockAlerts.HasValue
                    || x.OrderStatusAlerts.HasValue
                    || x.SubscriptionAlerts.HasValue
                    || x.EmailNotifications.HasValue
                    || x.InAppNotifications.HasValue)
            .WithMessage(
                "At least one notification preference field must be provided.")
            .OverridePropertyName("Request");
    }
}