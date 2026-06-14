using MediatR;

namespace Application.Features.Customer.Cart.Commands.UpdateQuantity;

public class UpdateQuantityCommand : IRequest<bool>
{
    public Guid CustomerAccountId { get; set; }
    public Guid ProductId { get; set; }
    public int NewQuantity { get; set; }
}