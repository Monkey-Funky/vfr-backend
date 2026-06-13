using Application.Features.Customer.Cart.DTOs;
using MediatR;

namespace Application.Features.Customer.Cart.Queries.GetCart;

public class GetCartQuery : IRequest<CartDto>
{
    public Guid CustomerAccountId { get; set; }
}