using Domain.Common;

namespace Domain.Entities.CustomerOrders;

public class Payment : BaseEntity
{
    public Guid COrderId { get; set; } // link payment to the order
    
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty; 
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    // Stripe Payment Gateway
    public string StripePaymentIntentId { get; set; } = string.Empty; 
}