namespace Application.Features.PaymentMethods.Commands.RemovePaymentMethod;

/// <summary>
/// Soft-deletes a payment method for the authenticated retailer.
/// Business Rule: Cannot remove the default card if other cards exist.
/// The retailer must designate another card as default first.
/// </summary>
public sealed record RemovePaymentMethodCommand(
    Guid PaymentMethodId
) : IRequest<Result<bool>>;