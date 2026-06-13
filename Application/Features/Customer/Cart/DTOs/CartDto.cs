namespace Application.Features.Customer.Cart.DTOs;

public class CartDto
{
    public Guid CartId { get; set; }
    public List<CartItemDto> Items { get; set; } = new();
    
    public int TotalQuantity => Items.Sum(i => i.Quantity);
    public decimal TotalPrice => Items.Sum(i => i.Total);
    public decimal Discount { get; set; } 
    public decimal ShippingCost { get; set; } 
    public decimal AmountPayable => (TotalPrice - Discount) + ShippingCost;
}