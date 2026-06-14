using Application.Interfaces.External;
using Microsoft.Extensions.Configuration;
using Stripe;

namespace Infrastructure.Services;

public class StripeService : IStripeService
{
    public StripeService(IConfiguration configuration)
    {
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    public async Task<(string ClientSecret, string PaymentIntentId)> CreatePaymentIntentAsync(decimal amount)
    {
        var options = new PaymentIntentCreateOptions
        {
            Amount = (long)(amount * 100), 
            Currency = "egp",
            PaymentMethodTypes = new List<string> { "card" },
        };

        var service = new PaymentIntentService();
        var intent = await service.CreateAsync(options);

        return (intent.ClientSecret, intent.Id);
    }
}