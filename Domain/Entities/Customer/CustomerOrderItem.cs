namespace Domain.Entities.Customer;

public class COrderItem : BaseEntity
{
    public Guid OrderId { get; set; } 
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalUnitPrice { get; set; }
}