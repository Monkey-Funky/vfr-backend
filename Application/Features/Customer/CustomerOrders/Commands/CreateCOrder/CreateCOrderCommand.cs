using MediatR;

namespace Application.Features.Customer.CustomerOrders.Commands.CreateCOrder;

public class CreateCOrderCommand : IRequest<Guid>
{
    public Guid CustomerAccountId { get; set; }

    public string DeliveryMethod { get; set; } = string.Empty;
    public Guid? ShippingAddressId { get; set; }

    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTimeFrame { get; set; }
    public string? OrderDescription { get; set; }
}