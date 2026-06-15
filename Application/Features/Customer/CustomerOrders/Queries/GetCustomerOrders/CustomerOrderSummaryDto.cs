namespace Application.Features.Customer.CustomerOrders.Queries.GetCustomerOrders;

public class CustomerOrderSummaryDto
{
    public Guid OrderId { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Discount { get; set; }
    public decimal ShippingCost { get; set; }
    public string DeliveryMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AddressSummary { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
}