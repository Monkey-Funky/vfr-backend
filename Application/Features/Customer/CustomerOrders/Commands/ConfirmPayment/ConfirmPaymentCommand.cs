using MediatR;

namespace Application.Features.Customer.CustomerOrders.Commands.ConfirmPayment;

public class ConfirmPaymentCommand : IRequest<bool>
{
    public Guid COrderId { get; set; }
}