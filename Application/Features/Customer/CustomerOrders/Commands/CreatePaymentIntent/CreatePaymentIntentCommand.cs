using MediatR;

namespace Application.Features.Customer.CustomerOrders.Commands.CreatePaymentIntent;

public class CreatePaymentIntentCommand : IRequest<string>
{
    public Guid COrderId { get; set; }
}