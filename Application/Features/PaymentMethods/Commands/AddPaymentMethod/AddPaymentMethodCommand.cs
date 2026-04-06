
namespace Application.Features.PaymentMethods.Commands.AddPaymentMethod;

/// <summary>
/// Adds a new payment method for the authenticated retailer.
/// The card must already be tokenized via Stripe Elements on the frontend —
/// StripePaymentMethodId (pm_xxxx) is the resulting token.
/// CardholderName is received in plaintext and encrypted by this handler.
/// </summary>
public sealed record AddPaymentMethodCommand(
    string ProviderType,
    string CardholderName,
    string CardNumberLast4,
    string ExpiryDate,
    string? StripePaymentMethodId,
    bool IsSaved = true,
    bool SetAsDefault = false
) : IRequest<Result<Guid>>;