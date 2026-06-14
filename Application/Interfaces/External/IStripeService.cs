namespace Application.Interfaces.External;

public interface IStripeService
{
        Task<(string ClientSecret, string PaymentIntentId)> CreatePaymentIntentAsync(decimal amount);
}