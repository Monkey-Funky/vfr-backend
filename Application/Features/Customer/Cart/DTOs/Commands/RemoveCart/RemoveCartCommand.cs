using MediatR;

namespace Application.Features.Customer.Cart.Commands.RemoveCart;

public class RemoveCartCommand : IRequest<bool>
{
    public Guid CustomerAccountId { get; set; }
}