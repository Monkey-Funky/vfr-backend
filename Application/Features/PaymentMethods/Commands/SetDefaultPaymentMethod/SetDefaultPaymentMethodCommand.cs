namespace Application.Features.PaymentMethods.Commands.SetDefaultPaymentMethod;

/// <summary>
/// Sets a payment method as the retailer's default/recurring card.
/// All other cards for this retailer are simultaneously un-set.
/// This command is atomic — both operations execute within a single transaction.
/// </summary>
public sealed record SetDefaultPaymentMethodCommand(
    Guid PaymentMethodId
) : IRequest<Result<bool>>;