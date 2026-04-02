namespace Application.Interfaces;

/// <summary>
/// Abstraction over the Stripe payment gateway.
/// Implemented in Infrastructure.Services.StripePaymentGatewayService.
/// 
/// The interface is defined in the Application layer so that command handlers
/// depend on the abstraction, not the concrete Stripe SDK. This enables
/// unit-testing of payment flows without real Stripe API calls.
/// </summary>
public interface IPaymentGatewayService
{
    /// <summary>
    /// Charges a saved Stripe payment method for the given amount.
    /// </summary>
    /// <param name="stripePaymentMethodId">
    ///     The Stripe PaymentMethod token (e.g. "pm_xxxx").
    ///     Retrieved from the PaymentMethod entity's StripePaymentMethodId field.
    /// </param>
    /// <param name="amount">Charge amount in the smallest currency unit (e.g. cents for USD).</param>
    /// <param name="currency">ISO 4217 currency code, lowercase (e.g. "usd").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     A <see cref="PaymentResult"/> indicating success or failure.
    ///     Never throws on Stripe failure — failures are reported via PaymentResult.Success = false.
    /// </returns>
    Task<PaymentResult> ChargeAsync(
        string stripePaymentMethodId,
        decimal amount,
        string currency,
        CancellationToken ct);
}

/// <summary>
/// Result of a payment gateway charge attempt.
/// </summary>
public sealed record PaymentResult(
    bool Success,
    string? StripePaymentIntentId,
    string? ErrorMessage
);