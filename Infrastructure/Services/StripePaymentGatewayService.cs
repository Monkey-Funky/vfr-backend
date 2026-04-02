using Application.Interfaces;
using Domain.Exceptions;
using Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Registry;
using Stripe;

namespace Infrastructure.Services;

/// <summary>
/// Implements IPaymentGatewayService using the Stripe .NET SDK.
///
/// Resilience: All Stripe API calls are executed through the Polly resilience
/// pipeline keyed "stripe". The pipeline is configured in P-049 (Infrastructure DI)
/// with retry (3 attempts, exponential backoff), circuit breaker, and timeout policies.
///
/// Error Handling:
///   - StripeException → wrapped in ExternalServiceException("Stripe", message).
///   - PaymentIntent status != "succeeded" → returns PaymentResult(Success: false).
///   - All Stripe errors are logged at Warning level with the payment method ID
///     (never the full card data).
///
/// The service is registered as Scoped to align with the request lifetime.
/// StripeConfiguration.ApiKey is set once per instance from IOptions{StripeSettings}.
/// </summary>
public sealed class StripePaymentGatewayService : IPaymentGatewayService
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<StripePaymentGatewayService> _logger;

    public StripePaymentGatewayService(
        ResiliencePipelineProvider<string> pipelineProvider,
        IOptions<StripeSettings> stripeOptions,
        ILogger<StripePaymentGatewayService> logger)
    {
        ArgumentNullException.ThrowIfNull(pipelineProvider);
        ArgumentNullException.ThrowIfNull(stripeOptions);

        _pipeline = pipelineProvider.GetPipeline("stripe");
        _logger = logger;

        // Set the global Stripe API key. IOptions ensures this is set once
        // per service instance using the correctly bound configuration value.
        StripeConfiguration.ApiKey = stripeOptions.Value.SecretKey;
    }

    /// <inheritdoc />
    public async Task<PaymentResult> ChargeAsync(
        string stripePaymentMethodId,
        decimal amount,
        string currency,
        CancellationToken ct)
    {
        // Convert amount to smallest currency unit (e.g., cents for USD).
        // Stripe requires amounts in the smallest unit — multiply by 100.
        long amountInSmallestUnit = (long)Math.Round(amount * 100, 0, MidpointRounding.AwayFromZero);

        _logger.LogInformation(
            "Initiating Stripe charge — Amount: {Amount} {Currency} | " +
            "PaymentMethod: {MethodId}",
            amount, currency.ToUpperInvariant(), stripePaymentMethodId);

        try
        {
            PaymentIntent intent = await _pipeline.ExecuteAsync(
                async token =>
                {
                    var service = new PaymentIntentService();

                    var options = new PaymentIntentCreateOptions
                    {
                        Amount = amountInSmallestUnit,
                        Currency = currency.ToLowerInvariant(),
                        PaymentMethod = stripePaymentMethodId,
                        Confirm = true,
                        OffSession = true, // Recurring / server-side charge
                    };

                    return await service.CreateAsync(options, cancellationToken: token);
                },
                ct);

            if (intent.Status == "succeeded")
            {
                _logger.LogInformation(
                    "Stripe charge succeeded — PaymentIntentId: {IntentId}",
                    intent.Id);

                return new PaymentResult(
                    Success: true,
                    StripePaymentIntentId: intent.Id,
                    ErrorMessage: null);
            }

            // Payment was not declined (no exception) but did not succeed.
            // Common for intents requiring additional authentication.
            _logger.LogWarning(
                "Stripe PaymentIntent {IntentId} completed with non-success status: {Status}",
                intent.Id, intent.Status);

            return new PaymentResult(
                Success: false,
                StripePaymentIntentId: intent.Id,
                ErrorMessage: $"Payment not completed. Stripe status: {intent.Status}.");
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(
                ex,
                "Stripe API error for PaymentMethod {MethodId} — " +
                "Code: {Code} | DeclineCode: {DeclineCode} | Message: {Message}",
                stripePaymentMethodId,
                ex.StripeError?.Code,
                ex.StripeError?.DeclineCode,
                ex.Message);

            // Re-wrap in domain exception so handlers remain unaware of Stripe SDK types.
            throw new ExternalServiceException(
                "Stripe",
                ex.StripeError?.Message ?? ex.Message);
        }
    }
}