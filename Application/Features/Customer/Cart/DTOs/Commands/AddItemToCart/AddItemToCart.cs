using MediatR;

namespace Application.Features.Customer.Cart.Commands.AddItemToCart;

public class AddItemToCartCommand : IRequest<bool>
{
    public Guid CustomerAccountId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}