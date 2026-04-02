namespace Application.Features.PaymentMethods.Queries.GetPaymentMethods;

/// <summary>
/// Returns all active (non-deleted) payment methods for the authenticated retailer.
/// Results include decrypted cardholder names — handled internally by the handler.
/// </summary>
public sealed record GetPaymentMethodsQuery : IRequest<IReadOnlyList<PaymentMethodDto>>;
