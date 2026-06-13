namespace Domain.Entities.Customer;

public class COrder : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid CustomerAccountId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = "Pending";
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public string? OrderDescription { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal GrandTotal { get; set; }
    public string ShippingMethod { get; set; } = string.Empty;
    public decimal MethodCost { get; set; }
    public DateTime? DeliveryDate { get; set; }

    public ICollection<COrderItem> OrderItems { get; set; } = new List<COrderItem>();
}