using Domain.Common;

namespace Domain.Entities.CustomerOrders;

public class COrderItem : BaseEntity
{
    public Guid COrderId { get; set; } // Fk
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalUnitPrice { get; set; }
    
    public string? CarrierName { get; set; }
    public string? ShippingAddress { get; set; }
    public string Size { get; set; } = string.Empty;
}