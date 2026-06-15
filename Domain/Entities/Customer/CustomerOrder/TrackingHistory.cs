using Domain.Common;

namespace Domain.Entities.CustomerOrders;

public class TrackingHistory : BaseEntity
{
    public Guid COrderId { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    // ("Order placed", "Processing", "Shipped", "Out for Delivery", "Delivered")
    public string Status { get; set; } = string.Empty; 
    
    // ("Online", "Fulfillment center", "Warehouse New York")
    public string Location { get; set; } = string.Empty;
}