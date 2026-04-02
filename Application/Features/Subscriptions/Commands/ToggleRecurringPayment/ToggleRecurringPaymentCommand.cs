namespace Application.Features.Subscriptions.Commands.ToggleRecurringPayment;

/// <summary>
/// Toggles automatic recurring billing on or off for the retailer's subscription.
/// When IsRecurringEnabled = false, the RecurringPaymentJob will skip this retailer.
/// </summary>
public sealed record ToggleRecurringPaymentCommand : IRequest<Result<bool>>;