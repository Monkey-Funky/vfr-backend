using Domain.Common;

namespace Domain.Entities.Customer;

public class Cart : BaseEntity
{
    public Guid CustomerAccountId { get; set; } // GUID is a unique digital fingerprint for data
    public CustomerAccount CustomerAccount { get; set; } = null!;

    public string Status { get; set; } = "Active"; 
    public decimal AmountPayable { get; set; }
    public int TotalQuantity { get; set; }
    public decimal TotalPrice { get; set; }
    
    public List<CartItem> Items { get; set; } = new();

   public void CalculateTotals()
    {
        if (Items != null && Items.Any())
        {
            TotalQuantity = Items.Sum(i => i.Quantity);
            TotalPrice = Items.Sum(i => i.Quantity * i.UnitPrice);
            
            AmountPayable = TotalPrice; 
        }
        else
        {
            TotalQuantity = 0;
            TotalPrice = 0;
            AmountPayable = 0;
        }
    }
}