using MediatR;

namespace Application.Features.Customer.Cart.Commands.RemoveItemFromCart;

public class RemoveItemFromCartCommand : IRequest<bool>
{
    public Guid CustomerAccountId { get; set; }
    public Guid ProductId { get; set; }
}